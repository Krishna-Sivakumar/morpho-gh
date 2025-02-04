using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Rhino;

using Grasshopper.Kernel;
using System.Data.SQLite;
using System.Timers;
using System.Diagnostics;

namespace ghplugin
{
    using NumberSlider = Grasshopper.Kernel.Special.GH_NumberSlider;

    public delegate void ExpireSolutionDelegate(Boolean recompute);

    class ParameterException : Exception { }

    public struct MorphoSolution
    {
        public Dictionary<string, double> values;
    }

    public struct SolutionSetParameters {
        public string directory, projectName, fitnessConditions;
    }

    public class GeneGenerator : GH_Component
    {
        private struct NamedMorphoInterval
        {
            public double start;
            public double end;
            public bool is_constant;
            public string nickname;
        }
        private AlgorithmParameterSet algorithmParameters;
        private bool is_systematic = false;
        private int seed;
        private Dictionary<int, Random> randomGenerators;
        

        // Timer Section

        /// <summary>Tracks all the details needed to iterate the component.</summary>
        private struct IterationStats
        {
            public long iterationLimit;
            public long iterationCount;
            public string directory;    // directory of the sqlite DB
            public string projectName;  // name of the project within the DB
            public Timer timer;         // actual timer object doing the heavy lifting
            public bool expired;        // we reset this to false only when the component's toggle is reset. makes sure that new iterations don't start on their own.
        };
        private IterationStats iterationStats;

        private long getCurrentDBIteration(string directory, string projectName)
        {
            try
            {
                var connBuilder = new SQLiteConnectionStringBuilder();
                connBuilder.DataSource = $"{directory}/solutions.db";
                connBuilder.Version = 3;
                connBuilder.JournalMode = SQLiteJournalModeEnum.Wal;
                connBuilder.LegacyFormat = false;
                connBuilder.Pooling = true;

                using (SQLiteConnection conn = new SQLiteConnection(connBuilder.ToString()))
                {
                    conn.Open();
                    string query = $"select COUNT(*) from {projectName}";
                    var command = conn.CreateCommand();
                    command.CommandText = query;
                    return (long)command.ExecuteScalar();
                }
            }
            catch (SQLiteException e)
            {
                // database is locked, return the current iteration.
                if (e.ErrorCode == (int)SQLiteErrorCode.Busy)
                {
                    return iterationStats.iterationCount;
                }
                else
                {
                    throw e;
                }
            }
        }

        private void setTimerExpired(Object src, ElapsedEventArgs e)
        {
            // callback for the inprocess timer to check if the db has been updated

            // check if the count of elements were advanced and then restart this component if they have.
            var currentIterationCount = getCurrentDBIteration(iterationStats.directory, iterationStats.projectName);
            if (currentIterationCount > this.iterationStats.iterationCount)
            {
                Console.WriteLine("current iteration:");
                Console.WriteLine(currentIterationCount);
                iterationStats.iterationCount = currentIterationCount;
                if (currentIterationCount < iterationStats.iterationLimit)
                {
                    iterationStats.timer.Interval = 1000; //ms
                    iterationStats.timer.Start();
                    Console.WriteLine("iteration limit:");
                    Console.WriteLine(iterationStats.iterationLimit);
                }
                else
                {
                    // we nullify the timer, so that re-enabling the generator restarts the timer
                    iterationStats.timer = null;
                    return;
                }

                // Cannot call ExpireSolution(true) directly if it's not a UI component. So we do this.
                // Refer to https://discourse.mcneel.com/t/system-invalidoperationexception-cross-thread-operation-not-valid/95176.
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
            iterationStats.iterationCount = this.getCurrentDBIteration(iterationStats.directory, iterationStats.projectName);
            iterationStats.timer = new Timer(1000); //1s
            iterationStats.timer.AutoReset = false;
            iterationStats.timer.Elapsed += setTimerExpired;
            iterationStats.timer.Start();
        }

        private void StopTimer()
        {
            iterationStats.iterationCount = 0;
            if (iterationStats.timer != null)
            {
                iterationStats.timer.Stop();
            }
            iterationStats.timer = null;
        }

        // End Timer Section

        private IList<IGH_Param> GetSources(string name) {
            foreach (var param in Params.Input) {
                if (param.Name == name) {
                    return param.Sources;
                }
            }
            throw new Exception($"Could not find the field denoted by {name}");
        }

        private HashSet<string> getInputParameters(string projectName, SQLiteConnection DBConnection)
        {
            string getTableLayoutQuery = "SELECT parameters FROM project_layout WHERE project_name=$projectName";

            var command = DBConnection.CreateCommand();
            command.CommandText = getTableLayoutQuery;
            command.Parameters.AddWithValue("$projectName", projectName);
            using (var layoutReader = command.ExecuteReader())
            {
                var inputParameters = new HashSet<string>();
                if (layoutReader.Read())
                {
                    inputParameters = JsonConvert.DeserializeObject<HashSet<string>>(layoutReader.GetString(0));
                }
                return inputParameters;
            }
        }

        /// <summary>
        /// Fetches a number of valid solutions from the database satisfying fitnessConditions.
        /// </summary>
        /// <param name="fitnessConditions">An SQL condition set assembled by the FitnessFilter components.</param>
        /// <param name="directory">Struct containing the directory path and project name.</param>
        /// <returns></returns>
        protected MorphoSolution[] GetSolutionSet(string fitnessConditions, DirectoryParameters directory)
        {
            List<MorphoSolution> solutions = new List<MorphoSolution>();

            var connBuilder = new SQLiteConnectionStringBuilder();
            connBuilder.DataSource = $"{directory.directory}/solutions.db";
            connBuilder.Version = 3;
            connBuilder.JournalMode = SQLiteJournalModeEnum.Wal;
            connBuilder.LegacyFormat = false;
            connBuilder.Pooling = true;

            // 1. make a connection to directory/solutions.db
            using (var DBConnection = new SQLiteConnection(connBuilder.ToString()))
            {
                DBConnection.Open();

                // 2. form the query
                var query = "";
                if (fitnessConditions.Trim().Length > 0)
                {
                    // TODO SQL CHANGE
                    query = $"SELECT data FROM {directory.projectName} WHERE {fitnessConditions};";
                }
                else
                {
                    // TODO SQL CHANGE
                    // if the fitness function is empty, select everything
                    query = $"SELECT data FROM {directory.projectName};";
                }

                var inputParameters = getInputParameters(directory.projectName, DBConnection);

                // 3. make the query
                var command = DBConnection.CreateCommand();
                command.CommandText = query;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // 4. deserialize and store the results in an array
                        var json_object = reader.GetString(0);
                        var culledSolution = JsonConvert.DeserializeObject<SaveToDisk.SerializableSolution>(json_object).parameters;
                        // remove non-input fields
                        foreach (KeyValuePair<string, double> pair in culledSolution)
                        {
                            if (!inputParameters.Contains(pair.Key))
                            {
                                culledSolution.Remove(pair.Key);
                            }
                        }
                        solutions.Add(new MorphoSolution { values = culledSolution });
                    }
                }
            }
            return solutions.ToArray();
        }

        /// <summary>
        /// Creates a gene generation cycle that runs for a set amount of iterations.
        /// </summary>
        public GeneGenerator()
          : base("Gene Generator", "gg",
            "Generates Genes for the next Cycle",
            "Morpho", "Genetic Search")
        {
            // schema = new Dictionary<string, string> { };
            seed = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            randomGenerators = new Dictionary<int, Random>();
        }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // Use the pManager object to register your input parameters.
            // You can often supply default values when creating parameters.
            // All parameters must have the correct access type. If you want 
            // to import lists or trees of values, modify the ParamAccess flag.
            pManager.AddBooleanParameter("Is Systematic", "is_systematic", "Is the gene generation systematic? i.e., Are genes generation using an existing solution set?", GH_ParamAccess.item);
            pManager.AddNumberParameter("Seed", "seed", "Seed for random generation.", GH_ParamAccess.item);
            pManager.AddTextParameter("Algorithm Parameters", "params", "Parameters for Generating Genes.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Initiate", "initiate", "Initiates the Gene Generator.", GH_ParamAccess.item);
            pManager.AddTextParameter("Fitness Filters", "fitness_filters", "Conditions to filter the population by", GH_ParamAccess.item);
            pManager.AddTextParameter("Intervals", "intervals", "Set of Intervals for each input.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Iteration Limit", "iterations", "Number of iterations to run the generator for.", GH_ParamAccess.item);
            pManager.AddTextParameter("Directory", "Directory", "Directory to save generations results under. Should be connected to the Directory creature.", GH_ParamAccess.item);
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Genes", "genes", "List of Genes", GH_ParamAccess.list);
            pManager.AddTextParameter("Genes with Names", "genes_with_names", "List of Genes with associated names", GH_ParamAccess.list);
        }

        double generateRandomDouble(double start, double end)
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

        int generateRandomInt(int start, int end)
        {
            // random generators are preserved through runs
            if (!randomGenerators.ContainsKey(this.seed))
            {
                randomGenerators.Add(this.seed, new Random(this.seed));
            }
            var generator = randomGenerators[this.seed];
            var modifier = generator.Next() % (end - start);
            var result = start + modifier;
            randomGenerators[this.seed] = generator;
            return result;
        }

        /// <summary>
        /// Generate a solution
        /// </summary>
        /// <param name="DA"></param>
        private void GenerateSolution(IGH_DataAccess DA, MorphoSolution[] solutionSet, Dictionary<string, NamedMorphoInterval> intervals) {
            Dictionary<string, double> outputs = new Dictionary<string, double>();
            int seed = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds % int.MaxValue;
            Random generator = new Random(seed);
            int parent1 = -1, parent2 = -1;
            if (solutionSet.Length > 0)
            {
                parent1 = generator.Next() % solutionSet.Length;
                parent2 = generator.Next() % solutionSet.Length;
            }

            // foreach (KeyValuePair<string, string> kv in schema)
            foreach (KeyValuePair<string, NamedMorphoInterval> interval in intervals)
            {
                // string param_name = kv.Key;
                string param_name = interval.Key;
                // HUX
                var variant = generateRandomInt(0, 3);
                var random_treshold = generateRandomDouble(0, 1);

                if (!is_systematic || random_treshold < algorithmParameters.probability_mutation || parent1 == -1 || parent2 == -1)
                {
                    // generate if system is not systematic, if it mutates by chance or if there are not parents
                    double point_on_scale;
                    if (is_systematic)
                    {
                        // TODO fix nonsencical generation code
                        point_on_scale = generateRandomDouble(intervals[param_name].start, intervals[param_name].end);
                    }
                    else
                    {
                        point_on_scale = generateRandomDouble(intervals[param_name].start, intervals[param_name].end);
                    }
                    outputs.Remove(param_name);
                    outputs.Add(param_name, point_on_scale);
                }
                else if (is_systematic)
                {
                    // only use the solution set if the generation is systematic
                    if (variant == 0)
                    {
                        outputs.Remove(param_name);
                        outputs.Add(param_name, solutionSet[parent1].values[param_name]);
                    }
                    else if (variant == 1)
                    {
                        outputs.Remove(param_name);
                        outputs.Add(param_name, solutionSet[parent2].values[param_name]);
                    }
                    else if (variant == 2)
                    {
                        // crossover
                        outputs.Remove(param_name);
                        outputs.Add(param_name, (solutionSet[parent1].values[param_name] + solutionSet[parent2].values[param_name]) / 2);
                    }
                }
            }

            // set the ouput parameters
            double[] output_doubles = new double[outputs.Count];
            string[] output_human = new string[outputs.Count];
            var index = 0;
            foreach (var outputPair in outputs)
            {
                output_doubles[index] = outputPair.Value;
                output_human[index] = $"{outputPair.Key},{outputPair.Value}";
                index++;
            }
            DA.SetDataList(0, output_doubles);
            DA.SetDataList(1, output_human);
        }

        private Dictionary<string, NamedMorphoInterval> CollectIntervals(IList<IGH_Param> intervalSources, List<string>  encodedIntervals) {
            Dictionary<string, NamedMorphoInterval> intervals = new Dictionary<string, NamedMorphoInterval>();
            // we associate each interval input with the source component of the intervals to fetch the nickname
            for (int encodedIntervalIndex = 0; encodedIntervalIndex < encodedIntervals.Count; encodedIntervalIndex ++) {
                MorphoInterval temp_interval = JsonConvert.DeserializeObject<MorphoInterval>(encodedIntervals[encodedIntervalIndex]);
                var nickname = intervalSources[encodedIntervalIndex].NickName;
                intervals.Add(nickname, new NamedMorphoInterval
                {
                    nickname = nickname,
                    start = temp_interval.start,
                    end = temp_interval.end,
                    is_constant = temp_interval.is_constant
                });
            }
            return intervals;
        }

        private static void checkError(bool success)
        {
            if (!success)
                throw new ParameterException();
        }

        private static List<T> GetParameterList<T>(IGH_DataAccess DA, string fieldName)
        {
            List<T> data_items = new List<T>();
            checkError(DA.GetDataList(fieldName, data_items));
            return data_items;
        }

        private static T GetParameter<T>(IGH_DataAccess DA, string fieldName)
        {
            T data_item = default;
            checkError(DA.GetData(fieldName, ref data_item));
            return data_item;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            try
            {
                try
                {
                    string parameterString = GetParameter<string>(DA, "Algorithm Parameters");
                    algorithmParameters = JsonConvert.DeserializeObject<AlgorithmParameterSet>(parameterString);
                }
                catch
                {
                    // if there are no parameters provided, set them to default.
                    algorithmParameters.Default();
                }

                // we get the parameters needed to fetch the solution set from the local database
                string fitnessFilterString = GetParameter<string>(DA, "Fitness Filters");
                string directoryString = GetParameter<string>(DA, "Directory");
                var directoryParameters = JsonConvert.DeserializeObject<DirectoryParameters>(directoryString);
                MorphoSolution[] solutionSet = GetSolutionSet(fitnessFilterString, directoryParameters);

                // collect intervals from the input and dump them into a variable
                var intervalSources = GetSources("Intervals");
                var encodedIntervals = GetParameterList<string>(DA, "Intervals");
                var intervals = CollectIntervals(intervalSources, encodedIntervals);

                // collect iteration details and start the timer
                if (!iterationStats.expired)
                {
                    iterationStats.directory = directoryParameters.directory;
                    iterationStats.projectName = directoryParameters.projectName;
                    int tempIterationLimit = GetParameter<int>(DA, "Iteration Limit");
                    iterationStats.iterationLimit = getCurrentDBIteration(iterationStats.directory, iterationStats.projectName) + (long)tempIterationLimit;
                    iterationStats.expired = true;
                    StartTimer();
                }


                // start computing the solutions
                bool initiateComputation = GetParameter<bool>(DA, "Initiate");
                Dictionary<string, double> outputs = new Dictionary<string, double>();
                if (initiateComputation && iterationStats.iterationCount < iterationStats.iterationLimit)
                {
                    GenerateSolution(DA, solutionSet, intervals);
                }
                else
                {
                    // resetting the timer on a toggle switch
                    iterationStats.expired = false;
                    StopTimer();
                }
            }
            catch (ParameterException)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Missing Parameters");
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("5087ac2c-60e4-418d-b058-2dd08268a8d6");
    }

    public class GeneGeneratorOutput : GH_Component
    {
        /// <summary>
        /// Output from the gene generator is split into multiple outputs by this component.
        /// </summary>
        public GeneGeneratorOutput()
          : base("GGOutput", "Gene Generator Output", "Demultiplexes output from the Gene Generator", "Morpho", "Genetic Search")
        {
            this.MutableNickName = true;
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Input", "Input", "Set of outputs created by the Gene Generator", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Output", "output", "Multiplexed output", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string selfNickname = this.NickName;
            string generatorOutput = "";
            if (!DA.GetData(0, ref generatorOutput))
            {
                return;
            }

            var outputs = JsonConvert.DeserializeObject<Dictionary<string, double>>(generatorOutput);
            if (!outputs.ContainsKey(selfNickname))
            {
                return;
            }
            else
            {
                DA.SetData(0, outputs[selfNickname]);
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("601d646f-f90f-4654-beac-de8b6bf47462");
    }
}