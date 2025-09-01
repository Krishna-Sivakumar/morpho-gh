using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;

namespace morpho
{
    public struct MorphoAggregatedData
    {
        /// <summary> Pairs of input names to input values </summary>
        public Dictionary<string, double> inputs;
        /// <summary> Pairs of output names to output values </summary>
        public Dictionary<string, double> outputs;
        /// <summary>Pairs of file tags to file contents</summary>
        public Dictionary<string, string> files;  
        /// <summary>Pairs of image tags to <see cref="NamedBitmap"/></summary>
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
            pManager.AddGenericParameter("Outputs", "Outputs", "Set outputs. Set up a named Data component to name each output appropriately.", GH_ParamAccess.tree);
            var imgIndex = pManager.AddGenericParameter("Images", "Images", "Image files generated during a run.", GH_ParamAccess.list);
            var fileIndex = pManager.AddTextParameter("Files", "Files", "Textual output to be saved to files.", GH_ParamAccess.list);
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

        /// <summary>
        /// Takes the sources of a parameter, maps each source (component object) to whether it has data ready at its volatile output field (boolean),
        /// and checks if all sources have their volatile fields filled (boolean).
        /// </summary>
        private static bool IsFulfilled(IGH_Param param)
        {
            if (param.SourceCount > 0)
            {
                return param.Sources
                    .Select(src => src.VolatileDataCount > 0)
                    .Aggregate((acc, incoming) => acc & incoming);
            }
            else
            {
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

                /*
                    Iterating through each source of the output to collect data.
                    We do this as the output parameter can present as a tree, but we will still need to collect nicknames.
                    The source nicknames cannot be correlated to the tree based on the order anymore, 
                    so we'll have to visit each component's volatile data parameter to collect data.
                */
                result.outputs = new Dictionary<string, double>();
                foreach (var source in Params.Input[1].Sources)
                {
                    if (source.NickName.ToLower().Contains("ignore"))
                    {
                        // skip any outputs that have "ignore" in their nickname.
                        // This is meant to be used with analytic components that produce images and such.
                        continue;
                    }

                    double outputValue;
                    int possibleIntValue;

                    var volatileData = source.VolatileData.AllData(false).ToArray();
                    if (volatileData.Length > 1) {
                        throw new ParameterException($"Output {source.NickName} must be a single number.");
                    }
                    if (!volatileData[0].CastTo(out outputValue)) {
                        if (!volatileData[0].CastTo(out possibleIntValue)) {
                            throw new ParameterException($"Output {source.NickName} must be a number.");
                        } else {
                            outputValue = possibleIntValue;
                        }
                    }
                    result.outputs.Add(source.NickName, outputValue);
                }

                // we ingest images here
                // we capture viewports to a bitmap 
                // refer to https://discourse.mcneel.com/t/capture-viewport-as-image-in-cache/137791/2 for implementation

                bool imagesPresent = false;
                List<ViewportDetails> viewportDetails = new List<ViewportDetails>();
                try
                {
                    viewportDetails = GetParameterList<ViewportDetails>(DA, "Images");
                    imagesPresent = true;
                }
                catch
                {
                    imagesPresent = false;
                }

                if (imagesPresent)
                {
                result.images = new List<NamedBitmap>();
                foreach (var viewportDetail in viewportDetails)
                {
                    if (!viewportDetail.IsValid())
                    {
                        throw new ParameterException($"{viewportDetail.name} does not have a viewport or file path set.");
                    }

                    try
                    {
                            result.images.Add(viewportDetail.GetNamedBitmap());
                    }
                    catch (Exception ex) when (ex is OutOfMemoryException || ex is ArgumentException)
                    {
                            throw new ParameterException($"{viewportDetail.name} points to a file path that is not an image ({viewportDetail.path}). Exception {ex.GetType()}: {ex.Message}");
                        }
                    }
                }

                // We capture the output of panels into files here.
                result.files = new Dictionary<string, string>();
                foreach (var source in Params.Input[3].Sources)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    var volatileData = source.VolatileData.AllData(false).ToArray();
                    foreach (var possibleString in volatileData)
                    {
                        stringBuilder.Append(possibleString.ToString());
                        stringBuilder.Append('\n');
                    }

                    result.files.Add(
                        source.NickName,
                        stringBuilder.ToString().TrimEnd()
                    );
                }

                DA.SetData(0, result);
            } catch (ParameterException e) {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, e.Message);
            } 
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override Bitmap Icon {
            get {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var stream = assembly.GetManifestResourceStream("ghplugin.icons.data_aggregator.png");
                return new Bitmap(stream);
            }
        }
        public override Guid ComponentGuid => new Guid("a6035d06-ea19-4809-9088-307ac9b62739");
    }
}
