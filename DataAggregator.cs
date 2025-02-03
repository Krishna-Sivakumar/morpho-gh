using System;
using System.Collections.Generic;
using Newtonsoft.Json;

using Grasshopper.Kernel;
using Rhino.Geometry;

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
        /// Aggregates inputs, outputs and file names into a singular data item
        /// Parameters:
        ///     Inputs:     A set of inputs. Connect output from the Gene Generator here directly.
        ///     Outputs:    A set of outputs. Set up a named Data component to name each output appropriately.
        ///     Images:     A set of image files.
        ///     Files:      Names of analysis files generated.
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

        private Dictionary<string, double>  ParseCsv(List<string> text) {
            Dictionary<string, double> result = new Dictionary<string, double>();
            foreach (string line in text) {
                var pair = line.Split(',');
                result.Add(pair[0], double.Parse(pair[1]));
            }
            return result;
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Inputs", "Inputs", "Set of inputs. Connect output from the Gene Generator here directly.", GH_ParamAccess.list);
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

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            MorphoAggregatedData result = new MorphoAggregatedData();
            List<string> input_csv = new List<string>();
            DA.GetDataList("Inputs", input_csv);
            result.inputs = ParseCsv(input_csv);
            Console.WriteLine(result.inputs.Count);

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

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("a6035d06-ea19-4809-9088-307ac9b62739");
    }
}