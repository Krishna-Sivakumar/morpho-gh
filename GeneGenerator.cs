using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Rhino;

using Grasshopper.Kernel;
using System.Timers;
using System.Linq;
using Rhino.UI;
using System.ComponentModel.DataAnnotations;

namespace morpho
{
    public delegate void ExpireSolutionDelegate(bool recompute);

    [Serializable]
    class ParameterException: Exception {
        public ParameterException(): base() {}
        public ParameterException(string message) : base (message) {}
        public ParameterException(string message, Exception innerException) : base (message, innerException) {}
    }

    /// <summary> Represents a set of input parameters associated with a solution. </summary>
    public struct MorphoSolution
    {
        public Dictionary<string, double> values;
    }

    public class GeneGenerator : GH_Component
    {
        private AlgorithmParameterSet algorithmParameters;
        private bool is_systematic = false;
        private int seed;
        private Dictionary<int, Random> randomGenerators;
        

        /*

            Timer Section 

        */

        /// <summary>Tracks all the details needed to iterate the component.</summary>
        private struct IterationStats
        {
            public long iterationLimit;
            public long iterationCount; // how many iterations have occurred?
            public string directory;    // directory of the project
            public string projectName;  // name of the project within the DB
            public Timer timer;         // actual timer object doing the heavy lifting
            public bool expired;        // we reset this to false only when the component's toggle is reset. makes sure that new iterations don't start on their own.
        };
        private IterationStats iterationStats;

        /// <summary>
        /// Callback for when the in-component timer expires.
        /// Checks if there are more solutions in the database than before, and expires this component if that is the case (basically starting a recomputation).
        /// </summary>
        private void setTimerExpired(Object src, ElapsedEventArgs e)
        {
            DBOps dbOps = new DBOps(new DirectoryParameters{directory = iterationStats.directory, projectName = iterationStats.projectName});

            // check if the count of elements were advanced and then restart this component if they have.
            var currentIterationCount = dbOps.GetSolutionCount(iterationStats.projectName);
            if (currentIterationCount > this.iterationStats.iterationCount)
            {
                iterationStats.iterationCount = currentIterationCount;
                if (currentIterationCount >= iterationStats.iterationLimit) {
                    iterationStats.timer.Stop();
                    iterationStats.timer = null;
                    return;
                }

                /* 
                    Expire the solution.
                    We cannot call ExpireSolution(true) directly if it's not a UI component, so we do the following instead.
                    Refer to https://discourse.mcneel.com/t/system-invalidoperationexception-cross-thread-operation-not-valid/95176 for details.
                */
                var d = new ExpireSolutionDelegate(ExpireSolution);
                RhinoApp.InvokeOnUiThread(d, true);
            }
        }

        /// <summary>
        /// Initializes and starts the timer to advance the iteration automatically
        /// </summary>
        /// <param name="iterationLimit">Number of iterations</param>
        /// <param name="directory">Directory that contains the DB</param>
        /// <param name="projectName">Name of the current project</param>
        private void StartTimer()
        {
            DBOps ops = new DBOps(new DirectoryParameters{directory = iterationStats.directory, projectName = iterationStats.projectName});
            iterationStats.iterationCount = ops.GetSolutionCount(iterationStats.projectName);
            iterationStats.timer = new Timer(1000); //1s
            iterationStats.timer.AutoReset = true;
            iterationStats.timer.Elapsed += setTimerExpired;
            iterationStats.timer.Start();
        }

        private void StopTimer()
        {
            iterationStats.iterationCount = 0;
            if (iterationStats.timer != null) {
                iterationStats.timer.Stop();
            }
            iterationStats.timer = null;
        }

        /*

            End Timer Section.

        */

        /// <summary>
        /// Creates a gene generation cycle that runs for a set amount of iterations.
        /// </summary>
        public GeneGenerator()
          : base("Gene Generator", "Gene Generator",
            "Generates Genes for the next Cycle",
            "Morpho", "Genetic Search")
        {
            // schema = new Dictionary<string, string> { };
            seed = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            randomGenerators = new Dictionary<int, Random>();
        }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Is Systematic", "Is Systematic", "Is the gene generation systematic? i.e., Are genes generation using an existing solution set?", GH_ParamAccess.item);
            pManager.AddNumberParameter("Seed", "Seed", "Seed for random generation.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Algorithm Parameters", "Algorithm Parameters", "Parameters for Generating Genes.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Initiate", "Initiate", "Initiates the Gene Generator.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Fitness Filters", "Fitness Filters", "Conditions to filter the population by", GH_ParamAccess.item);
            pManager.AddGenericParameter("Intervals", "Intervals", "Set of Intervals for each input.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Iteration Limit", "Iterations", "Number of iterations to run the generator for.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Directory", "Directory", "Directory to save generations results under. Should be connected to the Directory creature.", GH_ParamAccess.item);
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Genes", "Genes", "List of Genes", GH_ParamAccess.list);
            pManager.AddTextParameter("Genes with Names", "Genes CSV", "List of Genes with associated names. Should be connected to Save To Disk's Input parameter.", GH_ParamAccess.list);
        }

        /// <summary>Flips an imaginary coin and returns if it is heads or tails.</summary>
        /// <returns>returns True if the result is positive, else returns false.</returns>
        private bool onCoinFlip() {
            return generateRandomDouble(0, 1) < 0.5;
        }

        private double generateRandomDouble(double start, double end, double step = 0)
        {
            // random generators are preserved through runs
            if (!randomGenerators.ContainsKey(this.seed))
            {
                randomGenerators.Add(this.seed, new Random(this.seed));
            }
            var generator = randomGenerators[this.seed];
            var modifier = generator.NextDouble();
            var result = start + (end - start) * modifier;
            randomGenerators[this.seed] = generator;
            return result;
        }

        private double uniformLine(double point1, double point2, double step) {
            var difference = Math.Abs(point1 - point2);
            // mu = (1 + 2 * sigma) * random() - sigma (or can be written as) mu = random() + (2 * random() - 1) * sigma
            var mu = (1 + 2 * algorithmParameters.spread_factor) * generateRandomDouble(0, 1) - algorithmParameters.spread_factor;
            if (point1 < point2) {
                // if step is 0, then we have an infinite amount of values. Just return the double.
                // otherwise, return the closest step to the value.
                var intermediate = point1 + difference * mu;
                return step > 0 ? Math.Floor(intermediate / step) * step : intermediate;
            } else {
                // if step is 0, then we have an infinite amount of values. Just return the double.
                // otherwise, return the closest step to the value.
                var intermediate = point2 + difference * mu;
                return step > 0 ? Math.Floor(intermediate / step) * step : intermediate;
            }
        }

        private Dictionary<string, double> GenerateSolution(MorphoSolution[] solutionSet, Dictionary<string, MorphoInterval> intervals) {
            var child = new Dictionary<string, double>();

            Random generator = new Random(seed);
            int parent1 = -1, parent2 = -1; // a parent being -1 means that it doesn't exist.
            if (solutionSet.Length > 0)
            {
                parent1 = generator.Next() % solutionSet.Length;
                parent2 = generator.Next() % solutionSet.Length;
            }

            // the amount of parents, ranges from 0 to 2.
            int parentCount = ((parent1 != -1) ? 1 : 0) + ((parent2 != -1) ? 1 : 0);

            // the clamp function to be used later on for breeding children from 2 parents
            Func<double, double, double, double> clamp = delegate(double min, double max, double value) {
                return Math.Max(min, Math.Min(max, value));
            };

            // iterate through each pair of {interval name , interval details} (start, end, step, etc.)
            foreach (var parameter in intervals)
            {
                string paramName  = parameter.Key;
                var paramInterval = parameter.Value;

                var random_treshold = generateRandomDouble(0, 1);
                if (!is_systematic || random_treshold < algorithmParameters.probability_mutation || parentCount == 0) {
                    // generate if system is not systematic, or if it mutates by chance, or if it does not possess 2 parents.
                    child.Add(
                        paramName,
                        generateRandomDouble(paramInterval.start, paramInterval.end, paramInterval.step)
                    );
                }
                else if (is_systematic) {       // use parents to derive solutions
                    if (parentCount == 1) {     // mutant child
                        var parent = solutionSet[Math.Max(parent1, parent2)];
                        double mutation = 0;
                        if (onCoinFlip()) {
                            var sign = onCoinFlip() ? 1 : -1;
                            mutation = sign * paramInterval.step;
                        }
                        child.Add(paramName, parent.values[paramName] + mutation);
                    } else {                    // breed child
                        if (parent1 == parent2) {
                            child.Add(paramName, solutionSet[parent1].values[paramName]);
                        } else {
                            child.Add( paramName, clamp(
                                paramInterval.start,
                                paramInterval.end,
                                uniformLine(solutionSet[parent1].values[paramName], solutionSet[parent2].values[paramName], paramInterval.step)
                            ) );
                        }
                    }
                }
            }

            return child;
        }

        /// <summary> Deserializes and returns intervals from a set of JSON-encoded interval strings. </summary>
        private Dictionary<string, MorphoInterval> CollectIntervals(List<MorphoInterval>  intervals) {
            Dictionary<string, MorphoInterval> dictionary = new Dictionary<string, MorphoInterval>();
            foreach (var interval in intervals) {
                dictionary.Add(interval.name, interval);
            }
            return dictionary;
        }

        /// <summary> Throws an exception with a message if the status flag passed in is false </summary>
        private static void checkError(bool status, string message)
        {
            if (!status)
                throw new ParameterException(message);
        }

        /// <summary>
        /// Returns a list of input values from a DataAccess slot
        /// </summary>
        private static List<T> GetParameterList<T>(IGH_DataAccess DA, string fieldName)
        {
            List<T> data_items = new List<T>();
            checkError(DA.GetDataList(fieldName, data_items), $"Missing parameter {fieldName}");
            return data_items;
        }

        /// <summary> 
        /// Returns a single input values from a DataAccess slot
        /// </summary>
        private static T GetParameter<T>(IGH_DataAccess DA, string fieldName)
        {
            T data_item = default;
            checkError(DA.GetData(fieldName, ref data_item), $"Missing parameter {fieldName}");
            return data_item;
        }

        /// <summary> Retrieve a value from a parameter field. If it is not present, return defaultValue instead. </summary>
        private static T GetParameter<T>(IGH_DataAccess DA, string fieldName, T defaultValue) {
            T data_item = default;
            if (DA.GetData(fieldName, ref data_item)) {
                return data_item;
            } else {
                return defaultValue;
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            try {
                
                // Generator isn't systematic by default.
                this.is_systematic = GetParameter(DA, fieldName: "Is Systematic", defaultValue: false); 

                // Use the current time as a seed if we don't get a seed value.
                this.seed = (int)GetParameter<double>(DA, 
                    fieldName: "Seed",
                    defaultValue: (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds
                );
                
                // if there are no parameters provided, set them to default.
                this.algorithmParameters = GetParameter(DA, "Algorithm Parameters", new AlgorithmParameterSet());

                /*
                    Collect intervals, and categorize them as input variables to be passed into the fitness filters.
                    Evaulate the expression tree collected from the fitness filter input.
                    The fitness filter tree needs a map from variables to their category (input or output) to produce the right query.
                    The final fitnessFilterString is passed into database calls to constraint the solution set.
                */
                var intervals = GetParameterList<MorphoInterval>(DA, "Intervals").ToDictionary(interval => interval.name, interval => interval);
                var paramLookupTable = intervals.Select(interval => interval.Value.name).ToDictionary(name => name, name => ParamType.Input);
                var fitnessFilter = GetParameter<FilterExpression>(DA, "Fitness Filters", new EmptyFilterExpression());
                var fitnessFilterString = fitnessFilter.Eval(paramLookupTable);
                var directoryParameters = GetParameter<DirectoryParameters>(DA, "Directory");

                Console.WriteLine($"{paramLookupTable.ToArray()} {fitnessFilter} {fitnessFilterString} {directoryParameters}");

                DBOps ops = new DBOps(directoryParameters);
                MorphoSolution[] solutionSet = ops.GetSolutions(directoryParameters.projectName, fitnessFilterString); //GetSolutionSet(fitnessFilterString, directoryParameters);
                Console.WriteLine($"got {solutionSet.Count()} solutions for the query {fitnessFilterString}");


                /*
                    StartTimer() starts a timer that re-executes this component every second.
                    When the timer expires, setTimerExpired() is called, which checks the number of solutions in the database.
                    If there are more solutions in the database than before, then an iteration has occurred.
                    If so, the component is expired and recomputed.
                */
                if (!iterationStats.expired)
                {
                    iterationStats.directory = directoryParameters.directory;
                    iterationStats.projectName = directoryParameters.projectName;
                    int tempIterationLimit = GetParameter<int>(DA, "Iteration Limit");
                    iterationStats.iterationLimit = ops.GetSolutionCount(directoryParameters.projectName) + (long)tempIterationLimit;

                    // we reset this to false only when the component's toggle is reset. makes sure that new iterations don't start on their own.
                    iterationStats.expired = true;
                    StartTimer();
                }


                // start computing the solutions
                bool initiateComputation = GetParameter<bool>(DA, "Initiate");
                Dictionary<string, double> outputs = new Dictionary<string, double>();
                if (initiateComputation && iterationStats.iterationCount <= iterationStats.iterationLimit)
                {
                    Dictionary<string, double> child;
                    do {
                        child = GenerateSolution(solutionSet, intervals);
                    } while (ops.CheckIfSolutionExists(directoryParameters.projectName, child));

                    // set the ouput parameters
                    var output_doubles  = child.Select(parameter => parameter.Value).ToArray();
                    var output_string   = child.Select(parameter => $"{parameter.Key},{parameter.Value}").ToArray();
                    DA.SetDataList(0, output_doubles);
                    DA.SetDataList(1, output_string);
                }
                else
                {
                    // clear out the random generators when the toggle is turned off
                    randomGenerators.Clear();
                    // resetting the timer on a toggle switch
                    iterationStats.expired = false;
                    StopTimer();
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Gene Generator is disabled.");
                    return;
                }
            }
            catch (ParameterException)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Missing Parameters");
                return;
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("5087ac2c-60e4-418d-b058-2dd08268a8d6");
    }
}
