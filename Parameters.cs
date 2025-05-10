using System;
using System.Drawing;
using Grasshopper.Kernel;

public class AlgorithmParameterSet
{
    public double probability_mutation;
    public double spread_factor;

    public AlgorithmParameterSet() {
        // mutation happens on a coin-flip
        probability_mutation = 0.5;
        // The outputs of a gene-combination lies on the line between the two genes, if spread_factor = 0. 
        // Any higher than 0 or any lower than -1 and it will move outside the line.
        spread_factor = 0;          
    }

    public override string ToString() {
        return $"Mutation Probability: {this.probability_mutation}\n Spread Factor: {this.spread_factor}";
    }
}

namespace morpho {
    public class Parameters : GH_Component
    {
        public Parameters() : base("Parameters", "Parameters", "Parameters for the Genetic Search", "Morpho", "Genetic Search") {}

        /// <summary>Registers all the input parameters for this component.</summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager) {
        pManager.AddNumberParameter("Mutation Probability", "Mutation Probability", "The chance that a new gene will be randomly produced.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Spread Factor", "Spread Factor", "Constant used to control the spread of children production. A value within [-1, 0] keeps a child gene between the parents. The child has a chance of falling outside, otherwise.", GH_ParamAccess.item);
        }

        /// <summary>Registers all the output parameters for this component.</summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager) {
        pManager.AddGenericParameter("Parameters", "Parameters", "Set of Parameters, Encoded", GH_ParamAccess.item);
        }

        /// <summary> If the statusFlag is set to false, raise an error. To be used with Grasshopper's DA methods. </summary>
        private static void checkError(bool statusFlag, string parameterName) {
        if (!statusFlag) throw new ParameterException(parameterName);
        }

        private static T GetParameter<T>(IGH_DataAccess DA, string fieldName) {
        T data_item = default;
        checkError(DA.GetData(fieldName, ref data_item), fieldName);
        return data_item;
        }

        /// <summary>This is the method that actually does the work.</summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
        AlgorithmParameterSet p = new AlgorithmParameterSet();
        try {
            p.probability_mutation = GetParameter<double>(DA, "Mutation Probability");
            p.spread_factor = GetParameter<double>(DA, "Spread Factor");
        } catch (ParameterException exception) {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"{exception.Message} is missing. A default value is assumed.");
        } finally {
            DA.SetData(0, p);
        }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override Bitmap Icon {
            get {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                if (false) {
                    // use this when you need to list out the names of embedded resources
                    string[] result = assembly.GetManifestResourceNames();
                    Console.WriteLine("manifest resources:");
                    foreach (var res in result) {
                        Console.WriteLine(res);
                    }
                }
                var stream = assembly.GetManifestResourceStream("ghplugin.icons.dna.png");
                return new Bitmap(stream);
            }
        }
        public override Guid ComponentGuid => new Guid("e0e098e6-0f5d-49f3-8eae-b6285862bdf3");
    }
}