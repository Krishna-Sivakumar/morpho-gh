using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;

namespace morpho
{
    public struct MorphoAggregatedData
    {
        public Dictionary<string, double> inputs;
        public Dictionary<string, double> outputs;
        public Dictionary<string, string> files; // pairs of <file tag, file name>
        public List<NamedBitmap> images;

        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }
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

        private Dictionary<string, double>  ParseCsv(List<string> text) {
            Dictionary<string, double> result = new Dictionary<string, double>();
            foreach (string line in text) {
                var pair = line.Split(',');
                result.Add(pair[0], double.Parse(pair[1]));
            }
            return result;
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Inputs", "Inputs", "Set of inputs. Connect output from the Gene Generator here directly.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Outputs", "Outputs", "Set outputs. Set up a named Data component to name each output appropriately.", GH_ParamAccess.tree);
            var imgIndex = pManager.AddGenericParameter("Images", "Images", "Image files generated during a run.", GH_ParamAccess.list);
            var fileIndex = pManager.AddTextParameter("Files", "Files", "Names of analysis files generated", GH_ParamAccess.list);
            pManager[imgIndex].Optional = true;
            pManager[fileIndex].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Aggregated Data", "Aggregated Data", "Aggregated output. Connect to the Morpho SaveToDisk component.", GH_ParamAccess.item);
        }

        private static void checkError(bool success, string message)
        {
            if (!success)
                throw new ParameterException(message);
        }

        private static List<T> GetParameterList<T>(IGH_DataAccess DA, string fieldName) {
            List<T> data_items = new List<T>();
            checkError(DA.GetDataList(fieldName, data_items), $"Missing parameter {fieldName}");
            return data_items;
        }

        private static GH_Structure<T> GetParameterTree<T>(IGH_DataAccess DA, string fieldName) where T: IGH_Goo {
            GH_Structure<T> tree;
            checkError(DA.GetDataTree(fieldName, out tree), $"Missing parameter {fieldName}");
            return tree;
        }

        private static bool IsFulfilled(IGH_Param param) {
            if (param.SourceCount > 0) {
                return param.Sources.Select(src => src.VolatileDataCount > 0).Aggregate((acc, incoming) => acc & incoming);
            } else {
                return true;
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            try {
                // returns false if any of the input sources to an input is empty
                Func<IGH_Param, bool> isFulfilled = (IGH_Param param) => IsFulfilled(param);

                // if any of the input sources is not fulfilled, the component does not execute
                if (!isFulfilled(this.Params.Input[0]) || !isFulfilled(this.Params.Input[1]) || !isFulfilled(this.Params.Input[2]) || !isFulfilled(this.Params.Input[3])) {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "One of the input sources did not fire.");
                    return;
                }


                MorphoAggregatedData result = new MorphoAggregatedData();
                var input_csv = GetParameterList<string>(DA, "Inputs");
                // if we get no inputs, we consider it empty (kinda obvious but GetDataList does not error when inputs are empty.)
                if (input_csv.Count == 0) {
                    throw new ParameterException();
                }

                try {
                    result.inputs = ParseCsv(input_csv);
                } catch {
                    throw new Exception("Input Parsing failed. Is it connected to the GeneGenerator?");
                }

                result.outputs = new Dictionary<string, double>();
                var outputTree = GetParameterTree<GH_Number>(DA, "Outputs");
                outputTree.Flatten();
                int sourceCounter = 0;
                foreach(var outputValue in outputTree.ToList())
                {
                    result.outputs.Add(
                        Params.Input[1].Sources[sourceCounter].NickName,
                        outputValue.Value
                    );
                    sourceCounter++;
                }

                // we ingest images here
                // we capture viewports to a bitmap 
                // refer to https://discourse.mcneel.com/t/capture-viewport-as-image-in-cache/137791/2 for implementation
                try {
                    var viewportDetails = GetParameterList<ViewportDetails>(DA, "Images");
                    result.images = new List<NamedBitmap>();
                    foreach (var viewportDetail in viewportDetails) {
                        var namedBitmap = new NamedBitmap{
                            bitmap = viewportDetail.viewport.CaptureToBitmap(),
                            name = viewportDetail.name
                        };
                        result.images.Add(namedBitmap);
                    }
                } catch (ParameterException) {
                    // do nothing if there are no Image Capture components provided.
                } catch (Exception e) {
                    Console.WriteLine(e);
                    throw new ParameterException("One of the inputs is not an Image Capture component.");
                }

                result.files = new Dictionary<string, string>();
                try {
                    var filesList = GetParameterList<string>(DA, "Files");
                    sourceCounter = 0;
                    foreach (string filename in filesList)
                    {
                        result.files.Add(
                            Params.Input[3].Sources[sourceCounter].NickName,
                            filename
                        );
                        sourceCounter++;
                    }
                } catch (Exception) {
                    // do nothing if there's an exception here
                }

                DA.SetData(0, result);
            } catch (ParameterException e) {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, e.Message);
            } 
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("a6035d06-ea19-4809-9088-307ac9b62739");
    }
}
