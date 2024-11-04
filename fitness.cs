using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Eto.Forms;
using Grasshopper.Kernel;
using Newtonsoft.Json;

namespace ghplugin
{

    public class Fitness : GH_Component
    {
        // private SqliteConnection DBConnection;

        private struct SerializableSolution
        {
            public Dictionary<string, double> inputs;
            public Dictionary<string, double> outputs;
        }

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public Fitness()
          : base("Fitness", "Fitness",
            "",
            "Morpho", "Genetic Search")
        {
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
            pManager.AddTextParameter("Aggregated Data", "Aggregated Data", "Aggregated data to be saved.", GH_ParamAccess.item);
            pManager.AddTextParameter("Directory", "Directory", "Directory where the data should be saved into.", GH_ParamAccess.item);
            pManager.AddTextParameter("Project Name", "Project Name", "Name of the project.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
        }

        /// <summary>
        /// DBConnection must be open while using this function. Expect errors otherwise.
        /// </summary>
        /// <param name="query"></param>
        // private int executeCreateQuery(string query)
        // {
        //     var command = DBConnection.CreateCommand();
        //     command.CommandText = query;
        //     return command.ExecuteNonQuery();
        // }

        // private int executeCreateQuery(string query, string projectName)
        // {
        //     var command = DBConnection.CreateCommand();
        //     command.CommandText = query;
        //     command.Parameters.AddWithValue("$projectName", projectName);
        //     return command.ExecuteNonQuery();
        // }

        private bool InputParameterCheck(MorphoAggregatedData solution, string directory, string projectName) {
            string path = $"{directory}/{projectName}_parameters.txt";
            FileInfo info = new FileInfo(path);
            if (!info.Exists) {
                // if the input parameters aren't written to disk already, write them now
                var inputParameters = new HashSet<string>();
                foreach (KeyValuePair<string, double> pair in solution.inputs) {
                    inputParameters.Add(pair.Key);
                }
                var writer = new StreamWriter(path);
                writer.Write(JsonConvert.SerializeObject(inputParameters));
                writer.Flush();
                writer.Close();
                return true;
            }
            var contents = File.ReadAllText(path);
            HashSet<string> existingInputParameters = JsonConvert.DeserializeObject<HashSet<string>>(contents);
            foreach (KeyValuePair<string, double> pair in solution.inputs) {
                if (!existingInputParameters.Contains(pair.Key)) {
                    return false;
                }
            }
            return true;
        }

        private string constructCSVHeader(MorphoAggregatedData aggregatedData) {
            List<string> header = new List<string>();

            foreach (KeyValuePair<string, double> inputPair in aggregatedData.inputs) {
                header.Add(inputPair.Key);
            }
            foreach (KeyValuePair<string, double> outputPair in aggregatedData.outputs) {
                header.Add(outputPair.Key);
            }
            foreach (KeyValuePair<string, string> filePair in aggregatedData.files) {
                header.Add(filePair.Key);
            }
            // TODO include image file headers

            return string.Join(",", header);
        }

        private void writeToCSV(string directory, string projectName, MorphoAggregatedData solution)
        {
            FileInfo info = new FileInfo($"{directory}/{projectName}.csv");
            var constructedHeader = constructCSVHeader(solution); // order: input parameter names, output parameter names, file tag names, image tag names
            if (!info.Exists) {
                StreamWriter csvHeaderWriter = new StreamWriter($"{directory}/{projectName}.csv");

                csvHeaderWriter.WriteLine(constructedHeader);
                csvHeaderWriter.Flush();

                csvHeaderWriter.Close();
            }

            var existingHeader = File.ReadLines($"{directory}/{projectName}.csv").First();
            if (!constructedHeader.Equals(existingHeader)) {
                StreamWriter csvHeaderWriter = new StreamWriter($"{directory}/{projectName}.csv");
                csvHeaderWriter.WriteLine(constructedHeader);
                csvHeaderWriter.Flush();
                csvHeaderWriter.Close();
            }

            StreamWriter csvWriter = new StreamWriter($"{directory}/{projectName}.csv", append: true);
            List<string> csvData = new List<string>();

            foreach (KeyValuePair<string, double> inputPair in solution.inputs) {
                csvData.Add(inputPair.Value.ToString());
            }
            foreach (KeyValuePair<string, double> outputPair in solution.outputs) {
                csvData.Add(outputPair.Value.ToString());
            }
            foreach (KeyValuePair<string, string> filePair in solution.files) {
                csvData.Add(filePair.Value);
            }
            // TODO write image file names

            csvWriter.WriteLine(string.Join(",", csvData));
            csvWriter.Flush();
            csvWriter.Close();
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string aggDataJson = "";
            DA.GetData(0, ref aggDataJson);
            string directory = "";
            DA.GetData(1, ref directory);
            string projectName = "";
            DA.GetData(2, ref projectName);

            // check if the directory is valid
            if (!Directory.Exists(directory)) {
                throw new Exception("Directory does not exist.");
            }

            MorphoAggregatedData solution = JsonConvert.DeserializeObject<MorphoAggregatedData>(aggDataJson);
            // SerializableSolution serializableSolution = new SerializableSolution
            // {
            //     inputs = solution.inputs,
            //     outputs = solution.outputs
            // };

            if (solution.inputs == null || solution.outputs == null) {
                // if anything turns null, return without failing
                return;
            }

            // TODO abort csv insertion in case SQL insertion fails
            if (!InputParameterCheck(solution, directory, projectName)) {
                throw new Exception("Input Parameters differ.");
            }
            writeToCSV(directory, projectName, solution);
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
        public override Guid ComponentGuid => new Guid("3c6102ac-8a0e-49fe-b82a-2b09f0b2acf6");
    }
}