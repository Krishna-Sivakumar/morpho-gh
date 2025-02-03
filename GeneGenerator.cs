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

    public struct MorphoSolution
    {
        public Dictionary<string, double> values;
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
        private ParameterSet parameters;
        // private Dictionary<string, string> schema;
        private bool is_systematic = false;
        private int seed;
        private Dictionary<int, Random> randomGenerators;
        private MorphoSolution[] solutionSet;
        private Dictionary<string, NamedMorphoInterval> intervals;

        // Timer Section

        /// <summary>Tracks all the details needed to iterate the component.</summary>
        private struct IterationStats {
            public long iterationLimit;
            public long iterationCount;
            public string directory;    // directory of the sqlite DB
            public string projectName;  // name of the project within the DB
            public Timer timer;         // actual timer object doing the heavy lifting
            public bool expired;        // we reset this to false only when the component's toggle is reset. makes sure that new iterations don't start on their own.
        };
        private IterationStats iterationStats;

        private long getCurrentDBIteration(string directory, string projectName) {
            try {
                var connBuilder = new SQLiteConnectionStringBuilder();
                connBuilder.DataSource = $"{directory}/solutions.db";
                connBuilder.Version = 3;
                connBuilder.JournalMode = SQLiteJournalModeEnum.Wal;
                connBuilder.LegacyFormat = false;
                connBuilder.Pooling = true;

                using (SQLiteConnection conn = new SQLiteConnection(connBuilder.ToString())) {
                    Console.WriteLine("database was opened by GeneGenerator.");
                    conn.Open();
                    string query = $"select COUNT(*) from {projectName}";
                    var command = conn.CreateCommand();
                    command.CommandText = query;
                    Console.WriteLine("database was closed by GeneGenerator.");
                    return (long)command.ExecuteScalar();
                }
            } catch(SQLiteException e) {
                // database is locked, return the current iteration.
                if (e.ErrorCode == (int)SQLiteErrorCode.Busy) {
                    return iterationStats.iterationCount;
                } else {
                    throw e;
                }
            }
        }

        private void setTimerExpired(Object src, ElapsedEventArgs e) {
            // callback for the inprocess timer to check if the db has been updated

            // check if the count of elements were advanced and then restart this component if they have.
            var currentIterationCount = getCurrentDBIteration(iterationStats.directory, iterationStats.projectName);
            if (currentIterationCount > this.iterationStats.iterationCount) {
                Console.Write("current iteration:");
                Console.WriteLine(currentIterationCount);
                // TODO something is screwed up here, check it
                iterationStats.iterationCount = currentIterationCount;
                if (iterationStats.iterationCount < iterationStats.iterationLimit) {
                    iterationStats.timer.Interval = 1000; //ms
                    iterationStats.timer.Start();
                    Console.WriteLine("Timer expired.");
                } else {
                    // we nullify the timer, so that re-enabling the generator restarts the timer
                    iterationStats.timer = null;
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
        private void StartTimer() {
            iterationStats.iterationCount = this.getCurrentDBIteration(iterationStats.directory, iterationStats.projectName);
            // doing this simplifies the iteration limit check in the future
            iterationStats.iterationLimit += iterationStats.iterationCount;

            iterationStats.timer = new Timer(1000); //1s
            iterationStats.timer.AutoReset = false;
            iterationStats.timer.Elapsed += setTimerExpired;
            iterationStats.timer.Start();
        }

        // End Timer Section

        /* 
        Commenting schema parsing out because intervals already does the job.
        Will have to check if this feature still stays relevant in the future before adding it in.
        Either ways, the way this is implemented right now is complete jank.

        /// <summary>
        /// Parses text into a schema, i.e. key-value pairs of parameter names to types
        /// </summary>
        /// <param name="rawText">text to be parsed goes here</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Dictionary<string, string> ParseSchema(string rawText)
        {
            Dictionary<string, string> schema = new Dictionary<string, string>();
            string[] fields = rawText.Split('\n');

            for (int i = 0; i < fields.Length; i++)
            {
                string token = fields[i].Trim();
                if (token.Length == 0)
                {
                    continue;
                }
                string[] interim_tokens = token.Split(',');
                if (interim_tokens.Length == 2)
                {
                    schema.Add(interim_tokens[0], interim_tokens[1]);
                }
                else
                {
                    throw new Exception("Invalid Schema Format");
                }
            }

            return schema;
        }
        */

        /// <summary>
        /// Creates a gene generation cycle that runs for a set amount of iterations.
        /// </summary>
        public GeneGenerator()
          : base("Gene Generator", "gg",
            "Generates Genes for the next Cycle",
            "Morpho", "Genetic Search")
        {
            // schema = new Dictionary<string, string> { };
            intervals = new Dictionary<string, NamedMorphoInterval>();
            seed = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            randomGenerators = new Dictionary<int, Random>();
        }

        private void checkError(bool success)
        {
            if (!success)
                throw new Exception("Parameters Missing.");
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
            pManager.AddTextParameter("Solution Set", "solution_set", "Set of Soultions to use for Gene Generation.", GH_ParamAccess.item);
            // pManager.AddTextParameter("Input Parameter Set", "Input Parameter Set", "Describes the set of input parameters and their data types.", GH_ParamAccess.item);
            pManager.AddTextParameter("Intervals", "intervals", "Set of Intervals for each input.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Iteration Limit", "iterations", "Number of iterations to run the generator for.", GH_ParamAccess.item);
            pManager.AddTextParameter("Directory", "directory", "Project directory to which the results are saved.", GH_ParamAccess.item);
            pManager.AddTextParameter("Project Name", "project_name", "Name of the project.", GH_ParamAccess.item);
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
            if (!randomGenerators.ContainsKey(this.seed)) {
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
            if (!randomGenerators.ContainsKey(this.seed)) {
                randomGenerators.Add(this.seed, new Random(this.seed));
            }
            var generator = randomGenerators[this.seed];
            var modifier = generator.Next() % (end - start);
            var result = start + modifier;
            randomGenerators[this.seed] = generator;
            return result;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string parameterString = "";
            if (!DA.GetData("Algorithm Parameters", ref parameterString))
            {
                // if there are no parameters provided, set them to default.
                parameters.Default();
            }
            parameters = JsonConvert.DeserializeObject<ParameterSet>(parameterString);


            /* Commenting this out because of the reason above.
            // string schemaString = "";
            // checkError(DA.GetData("Input Parameter Set", ref schemaString));
            // schema = ParseSchema(schemaString);
            */


            string solutionSetString = "[]";
            checkError(DA.GetData("Solution Set", ref solutionSetString));
            try
            {
                solutionSet = JsonConvert.DeserializeObject<MorphoSolution[]>(solutionSetString);
            }
            catch
            {
                // nothing happens if the solution set parsing fails
            }


            // collect intervals from the input and dump them into `intervals`
            List<string> encodedIntervals = new List<string>();
            checkError(DA.GetDataList("Intervals", encodedIntervals));
            intervals.Clear();
            int sourceCounter = 0;
            foreach (string encodedInterval in encodedIntervals)
            {
                MorphoInterval temp_interval = JsonConvert.DeserializeObject<MorphoInterval>(encodedInterval);
                var nickname = Params.Input[5].Sources[sourceCounter].NickName;
                intervals.Add(nickname, new NamedMorphoInterval
                {
                    nickname = Params.Input[5].Sources[sourceCounter].NickName,
                    start = temp_interval.start,
                    end = temp_interval.end,
                    is_constant = temp_interval.is_constant
                });
                sourceCounter++;
            }

            // collect iteration details and start the timer
            int tempIterationLimit = 0;
            checkError(DA.GetData("Directory", ref iterationStats.directory));
            checkError(DA.GetData("Project Name", ref iterationStats.projectName));
            checkError(DA.GetData("Iteration Limit", ref tempIterationLimit));
            iterationStats.iterationLimit = (long)tempIterationLimit;
            if (iterationStats.timer == null && !iterationStats.expired) {
                iterationStats.expired = true;
                StartTimer();
            }


            bool initiateComputation = false;
            checkError(DA.GetData("Initiate", ref initiateComputation));
            Dictionary<string, double> outputs = new Dictionary<string, double>();
            if (initiateComputation)
            {
                int seed = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds % int.MaxValue;
                Random generator = new Random(seed);
                int parent1 = -1, parent2 = -1;
                if (solutionSet.Length > 0) {
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

                    if (!is_systematic || random_treshold < parameters.probability_mutation || parent1 == -1 || parent2 == -1)
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
                foreach (var outputPair in outputs) {
                    output_doubles[index] = outputPair.Value;
                    output_human[index] = $"{outputPair.Key},{outputPair.Value}";
                    index ++;
                }
                DA.SetDataList(0, output_doubles);
                DA.SetDataList(1, output_human);
            } else {
                // resetting the timer on a toggle switch
                iterationStats.expired = false;
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
            if (!DA.GetData(0, ref generatorOutput)) {
                return;
            }

            var outputs = JsonConvert.DeserializeObject<Dictionary<string, double>>(generatorOutput);
            if (!outputs.ContainsKey(selfNickname)) {
                return;
            } else {
                DA.SetData(0, outputs[selfNickname]);
            }
            
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("601d646f-f90f-4654-beac-de8b6bf47462");
    }
}