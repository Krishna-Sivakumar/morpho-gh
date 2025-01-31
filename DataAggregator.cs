using System;
using System.Collections.Generic;
using Newtonsoft.Json;

using Grasshopper.Kernel;

namespace ghplugin
{
    public struct MorphoAggregatedData
    {
        public Dictionary<string, double> inputs;
        public Dictionary<string, double> outputs;
        public Dictionary<string, string> files; // pairs of <file tag, file name>
    }

    public class DataAggregator : GH_Component
    {

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public DataAggregator()
          : base("Data Aggregator", "Data Aggregator",
            "Aggregates inputs, outputs and analyses generated during a run.",
            "Morpho", "Genetic Search")
        {
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
            pManager.AddTextParameter("Inputs", "Inputs", "Set of inputs. Connect output from the Gene Generator here directly.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Outputs", "Outputs", "Set outputs. Set up a named Data component to name each output appropriately.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Images", "Images", "Image files generated during a run.", GH_ParamAccess.list);
            pManager.AddTextParameter("Files", "Files", "Names of analysis files generated", GH_ParamAccess.list);
            Params.Input[2].Optional = true;
            Params.Input[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Aggregated Data", "Aggregated Data", "Aggregated output. Connect to Morpho Data Access component.", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            MorphoAggregatedData result = new MorphoAggregatedData();
            string inputs_json = "";
            DA.GetData("Inputs", ref inputs_json);
            result.inputs = JsonConvert.DeserializeObject<Dictionary<string, double>>(inputs_json);

            result.outputs = new Dictionary<string, double>();
            List<double> outputList = new List<double>();
            int sourceCounter = 0;
            DA.GetDataList("Outputs", outputList);
            foreach (double outputValue in outputList)
            {
                result.outputs.Add(
                    Params.Input[1].Sources[sourceCounter].NickName,
                    outputValue
                );
                sourceCounter++;
            }

            result.files = new Dictionary<string, string>();
            List<string> filesList = new List<string>();
            sourceCounter = 0;
            DA.GetDataList("Files", filesList);
            foreach (string filename in filesList)
            {
                result.files.Add(
                    Params.Input[3].Sources[sourceCounter].NickName,
                    filename
                );
                sourceCounter++;
            }

            // TODO ingest images as well

            DA.SetData(0, JsonConvert.SerializeObject(result));
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
        public override Guid ComponentGuid => new Guid("a6035d06-ea19-4809-9088-307ac9b62739");
    }
}