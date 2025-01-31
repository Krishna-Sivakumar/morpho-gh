using System;
using System.Collections.Generic;
using Newtonsoft.Json;

using Grasshopper.Kernel;

namespace ghplugin
{
    using NumberSlider = Grasshopper.Kernel.Special.GH_NumberSlider;

    public struct MorphoSolution
    {
        public Dictionary<string, double> values;
    }

    public class GeneGenerator : GH_Component
    {
        private struct ParameterDefinition {
            public double max, min;
            public string name;
        }
        private ParameterDefinition[] parameterDefinitions;

        private struct NamedMorphoInterval
        {
            public double start;
            public double end;
            public bool is_constant;
            public string nickname;
        }
        private ParameterSet parameters;
        private Dictionary<string, string> schema;
        private bool is_systematic = false;
        private int seed;
        private Dictionary<int, Random> randomGenerators;
        private MorphoSolution[] solutionSet;
        private Dictionary<string, NamedMorphoInterval> intervals;

        private void collectInputSchema() {
            // Grasshopper.Kernel.Special.GH_NumberSlider component = (Grasshopper.Kernel.Special.GH_NumberSlider) this.Component.Params.Input[0].Sources[0];
            this.parameterDefinitions = new ParameterDefinition[this.Params.Input[0].Sources.Count];
            var index = 0;
            foreach (NumberSlider slider in this.Params.Input[0].Sources) {
                this.parameterDefinitions[index] = new ParameterDefinition();
                this.parameterDefinitions[index].max = (double) slider.Slider.Maximum;
                this.parameterDefinitions[index].min = (double) slider.Slider.Minimum;
                this.parameterDefinitions[index].name = slider.NickName;
                index ++;
            }
        }



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

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public GeneGenerator()
          : base("Gene Generator", "gg",
            "Generates Genes for the next Cycle",
            "Morpho", "Genetic Search")
        {
            schema = new Dictionary<string, string> { };
            intervals = new Dictionary<string, NamedMorphoInterval>();
            seed = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            randomGenerators = new Dictionary<int, Random>();
        }

        private void checkError(bool success)
        {
            if (!success)
                throw new Exception("Parameters Missing.");
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
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
            pManager.AddTextParameter("Input Parameter Set", "Input Parameter Set", "Describes the set of input parameters and their data types.", GH_ParamAccess.item);
            pManager.AddTextParameter("Intervals", "intervals", "Set of Intervals for each input.", GH_ParamAccess.list);
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

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string parameterString = "";
            if (!DA.GetData("Algorithm Parameters", ref parameterString))
            {
                // if there are no parameters provided, set them to default.
                parameters.Default();
            }
            parameters = JsonConvert.DeserializeObject<ParameterSet>(parameterString);


            string schemaString = "";
            checkError(DA.GetData("Input Parameter Set", ref schemaString));
            schema = ParseSchema(schemaString);


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


            // getting all the intervals here
            List<string> encodedIntervals = new List<string>();
            checkError(DA.GetDataList("Intervals", encodedIntervals));
            intervals.Clear();
            int sourceCounter = 0;
            foreach (string encodedInterval in encodedIntervals)
            {
                MorphoInterval temp_interval = JsonConvert.DeserializeObject<MorphoInterval>(encodedInterval);
                var nickname = Params.Input[6].Sources[sourceCounter].NickName;
                intervals.Add(nickname, new NamedMorphoInterval
                {
                    nickname = Params.Input[6].Sources[sourceCounter].NickName,
                    start = temp_interval.start,
                    end = temp_interval.end,
                    is_constant = temp_interval.is_constant
                });
                sourceCounter++;
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

                foreach (KeyValuePair<string, string> kv in schema)
                {
                    string param_name = kv.Key;
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

                double[] output_doubles = new double[outputs.Count];
                string[] output_human = new string[outputs.Count];
                var index = 0;
                foreach (var outputPair in outputs) {
                    output_doubles[index] = outputPair.Value;
                    output_human[index] = $"{outputPair.Key}, {outputPair.Value}";
                    index ++;
                }
                DA.SetDataList(0, output_doubles);
                DA.SetDataList(1, output_human);
            }
        }

        /// <summary>
        /// The Exposure property controls where in the panel a component icon 
        /// will appear. There are seven possible locations (primary to septenary), 
        /// each of which can be combined with the GH_Exposure.obscure flag, which 
        /// ensures the component will only be visible on panel dropdowns.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("5087ac2c-60e4-418d-b058-2dd08268a8d6");
    }

    public class GeneGeneratorOutput : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public GeneGeneratorOutput()
          : base("GGOutput", "Gene Generator Output", "Demultiplexes output from the Gene Generator", "Morpho", "Genetic Search")
        {
            this.MutableNickName = true;
        }

        private void checkError(bool success)
        {
            if (!success)
                throw new Exception("Parameters Missing.");
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // Use the pManager object to register your input parameters.
            // You can often supply default values when creating parameters.
            // All parameters must have the correct access type. If you want 
            // to import lists or trees of values, modify the ParamAccess flag.
            pManager.AddTextParameter("Input", "Input", "Set of outputs created by the Gene Generator", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Output", "output", "Multiplexed output", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
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

        /// <summary>
        /// The Exposure property controls where in the panel a component icon 
        /// will appear. There are seven possible locations (primary to septenary), 
        /// each of which can be combined with the GH_Exposure.obscure flag, which 
        /// ensures the component will only be visible on panel dropdowns.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("601d646f-f90f-4654-beac-de8b6bf47462");

    }
}