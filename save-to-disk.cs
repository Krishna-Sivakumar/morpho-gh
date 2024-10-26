using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using Eto.Forms;
using Grasshopper.Kernel;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace ghplugin
{

    public class SaveToDisk : GH_Component
    {
        private SqliteConnection DBConnection;

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
        public SaveToDisk()
          : base("Save to Disk", "Save to Disk",
            "Saves aggregated data to disk.",
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
        private int executeCreateQuery(string query)
        {
            var command = DBConnection.CreateCommand();
            command.CommandText = query;
            return command.ExecuteNonQuery();
        }

        private int executeCreateQuery(string query, string projectName)
        {
            var command = DBConnection.CreateCommand();
            command.CommandText = query;
            command.Parameters.AddWithValue("$projectName", projectName);
            return command.ExecuteNonQuery();
        }

        private void writeToCSV(string directory, string projectName, MorphoAggregatedData solution)
        {
            FileInfo info = new FileInfo($"{directory}/{projectName}.csv");
            var constructedHeader = ""; // order: input parameter names, output parameter names, file tag names, image tag names
            if (!info.Exists) {
                StreamWriter csvHeaderWriter = new StreamWriter($"{directory}/{projectName}.csv");

                // TODO write headers to the top before writing in the solution

                csvHeaderWriter.Close();
            }

            // TODO reconfigure header if it doesn't match with the existing header
            var existingHeader = File.ReadLines($"{directory}/{projectName}.csv").Last();
            if (!constructedHeader.Equals(existingHeader)) {
            }

            StreamWriter csvWriter = new StreamWriter($"{directory}/{projectName}.csv", append: true);
            List<string> csvData = new List<string>();

            foreach (KeyValuePair<string, double> inputPair in solution.inputs) {
                csvData.Append(inputPair.Value.ToString());
            }
            foreach (KeyValuePair<string, double> outputPair in solution.outputs) {
                csvData.Append(outputPair.Value.ToString());
            }
            foreach (KeyValuePair<string, string> filePair in solution.files) {
                csvData.Append(filePair.Value);
            }
            // TODO write image file names

            csvWriter.WriteLine(string.Join(",", csvData));
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

            MorphoAggregatedData solution = JsonConvert.DeserializeObject<MorphoAggregatedData>(aggDataJson);
            SerializableSolution serializableSolution = new SerializableSolution
            {
                inputs = solution.inputs,
                outputs = solution.outputs
            };

            // BEGIN Writing to Local DB

            DBConnection = new SqliteConnection($"Data Source={directory}/solutions.db");
            DBConnection.Open();

            // potential for SQL injection here... what do I do?
            string createTableLayoutQuery = "CREATE TABLE IF NOT EXISTS project_layout (project_name text primary key, parameters jsonb)";
            string createProjectTableQuery = "CREATE TABLE IF NOT EXISTS $projectName (data_id integer primary key auto increment, data jsonb not null)";
            string createAssetTableQuery = "CREATE TABLE IF NOT EXISTS $projectName_assets (asset_id integer primary key auto increment, data blob not null, tag text not null, data_id integer, foreign key(data_id) references {projectName}(data_id)";
            string getTableLayoutQuery = "SELECT parameters FROM project_layout WHERE project_name=$projectName";
            string insertSolutionQuery = "INSERT INTO $projectName (data) VALUES ($data)";
            string getSolutionIdQuery = "SELECT data_id FROM $projectName WHERE data = $data";
            string insertAssetQuery = $"INSERT INTO $projectName_assets(data, tag, data_id) values ($data, $tag, $data_id)";

            var status = executeCreateQuery(createTableLayoutQuery);
            // ERROR CHECKING REQUIRED

            status = executeCreateQuery(createProjectTableQuery, projectName);
            // ERROR CHECKING REQUIRED

            status = executeCreateQuery(createAssetTableQuery, projectName);
            // ERROR CHECKING REQUIRED

            // abort if any of the above fail

            var command = DBConnection.CreateCommand();
            command.CommandText = getTableLayoutQuery;
            var reader = command.ExecuteReader();

            // check if table layout differs for this insert. If it does, error out.
            if (reader.HasRows) {
                var parametersJson = reader.GetFieldValue<string>(0);
                List<string> parameters = new List<string>();
                // deserialize it into a list of input parameter names
                foreach (string parameter in parameters) {
                    if (!solution.inputs.ContainsKey(parameter)) {
                        throw new Exception("input parameters don't match with existing records.");
                    }
                }
            }

            bool hasErrored = false;
            SqliteTransaction transaction;

            using (transaction = DBConnection.BeginTransaction(IsolationLevel.Serializable)) {
                // insert solution first
                command = DBConnection.CreateCommand();
                command.CommandText = insertSolutionQuery;
                command.Parameters.AddWithValue("$projectName", projectName);
                command.Parameters.AddWithValue("$data", JsonConvert.SerializeObject(serializableSolution));
                status = command.ExecuteNonQuery();
                // ERROR CHECKING REQUIRED; write results to hasErrored

                // get solution id
                // for correctness' sake, get the id of the same object that was inserted
                command = DBConnection.CreateCommand();
                command.CommandText = getSolutionIdQuery;
                command.Parameters.AddWithValue("$projectName", projectName);
                command.Parameters.AddWithValue("$data", JsonConvert.SerializeObject(serializableSolution));
                int solutionId = (int)command.ExecuteScalar();
                // ERROR CHECKING REQUIRED; write results to hasErrored

                // insert assets for the particular solution
                foreach (KeyValuePair<string, string> asset in solution.files) {

                    // TODO terminate this if solution insertion fails
                    byte[] fileContents = File.ReadAllBytes(asset.Key);

                    command = DBConnection.CreateCommand();
                    command.CommandText = insertAssetQuery;
                    command.Parameters.AddWithValue("$projectName", projectName);
                    command.Parameters.AddWithValue("$data", fileContents).SqliteType = SqliteType.Blob;
                    command.Parameters.AddWithValue("$tag", asset.Key);
                    command.Parameters.AddWithValue("$data_id", solutionId);
                    status = command.ExecuteNonQuery();
                    // ERROR CHECKING REQUIRED; write results to hasErrored
                }
            }

            if (hasErrored) {
                transaction.Rollback();
            } else {
                transaction.Commit();
            }
            
            // END Writing to Local DB

            // TODO abort csv insertion in case SQL insertion fails

            writeToCSV(directory, projectName, solution);

            DBConnection.Close();
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