using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Data.SQLite;
using Grasshopper.Kernel;
using Newtonsoft.Json;

namespace ghplugin
{

    public class SaveToDisk : GH_Component
    {

        public struct SerializableSolution {
            public Dictionary<string, double> parameters;
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
            pManager.AddBooleanParameter("Looper", "Looper", "Connects to Looper to start the next iteration", GH_ParamAccess.item);
        }

        /// <summary>
        /// DBConnection must be open while using this function. Expect errors otherwise.
        /// </summary>
        /// <param name="query"></param>
        private int executeCreateQuery(string query, SQLiteConnection DBConnection)
        {
            var command = DBConnection.CreateCommand();
            command.CommandText = query;
            return command.ExecuteNonQuery();
        }

        private bool InputParameterCheck(MorphoAggregatedData solution, string projectName, SQLiteConnection DBConnection) {
            string getTableLayoutQuery      = "SELECT parameters FROM project_layout WHERE project_name=$projectName";
            string insertTableLayoutQuery   = "INSERT INTO project_layout VALUES ($projectName, $layout)";
            var inputParameters = new HashSet<string>();

            var getCommand = DBConnection.CreateCommand();
            getCommand.CommandText = getTableLayoutQuery;
            getCommand.Parameters.AddWithValue("$projectName", projectName);
            using (var layoutReader = getCommand.ExecuteReader()) {
                inputParameters = new HashSet<string>();
                if (layoutReader.Read()) {
                    var serializedInputParameters = layoutReader.GetString(0);
                    inputParameters = JsonConvert.DeserializeObject<HashSet<string>>(serializedInputParameters);
                    foreach (KeyValuePair<string, double> pair in solution.inputs) {
                        if (!inputParameters.Contains(pair.Key)) {
                            return false;
                        }
                    }
                    return true;
                }
            }

            // if the input parameters aren't written to disk already, write them now
            foreach (KeyValuePair<string, double> pair in solution.inputs) {
                inputParameters.Add(pair.Key);
            }
            var insertCommand = DBConnection.CreateCommand();
            insertCommand.CommandText = insertTableLayoutQuery;
            insertCommand.Parameters.AddWithValue("$projectName", projectName);
            insertCommand.Parameters.AddWithValue("$layout", JsonConvert.SerializeObject(inputParameters));
            insertCommand.ExecuteNonQuery();
            // TODO error handling
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

            projectName = System.Security.SecurityElement.Escape(projectName);

            // check if the directory is valid
            if (!Directory.Exists(directory)) {
                throw new Exception("Directory does not exist.");
            }

            MorphoAggregatedData solution = JsonConvert.DeserializeObject<MorphoAggregatedData>(aggDataJson);
            if (solution.inputs == null || solution.outputs == null) {
                // if aggregated data is null, stop executing the component
                return;
            }

            Dictionary<string, double> parameters = new Dictionary<string, double>();
            foreach (KeyValuePair<string, double> pair in solution.inputs) {
                parameters.Add(pair.Key, pair.Value);
            }
            foreach (KeyValuePair<string, double> pair in solution.outputs) {
                parameters.Add(pair.Key, pair.Value);
            }
            SerializableSolution serializableSolution = new SerializableSolution
            {
                parameters = parameters

            };

            if (solution.inputs == null || solution.outputs == null) {
                // if anything turns null, return without failing
                return;
            }

            // BEGIN Writing to Local DB

            var connBuilder = new SQLiteConnectionStringBuilder();
            connBuilder.DataSource = $"{directory}/solutions.db";
            connBuilder.Version = 3;
            connBuilder.JournalMode = SQLiteJournalModeEnum.Wal;
            connBuilder.LegacyFormat = false;
            connBuilder.Pooling = true;

            using (var DBConnection = new SQLiteConnection(connBuilder.ToString()))
            {
                Console.WriteLine("database was opened by SaveToDisk in WAL mode.");
                Console.WriteLine("starting save");
                DBConnection.Open();

                string createTableLayoutQuery   = "CREATE TABLE IF NOT EXISTS project_layout (project_name text primary key, parameters jsonb)";
                string insertTableLayoutQuery   = $"INSERT INTO project_layout VALUES ({projectName}, $layout)";
                string getTableLayoutQuery      = "SELECT parameters FROM project_layout WHERE project_name=$projectName";

                string createProjectTableQuery  = $"CREATE TABLE IF NOT EXISTS {projectName} (data_id integer primary key autoincrement, data jsonb not null)";
                string insertSolutionQuery      = $"INSERT INTO {projectName} (data) VALUES ($data)";
                string getSolutionIdQuery       = $"SELECT data_id FROM {projectName} WHERE data = $data";

                string createAssetTableQuery    = $"CREATE TABLE IF NOT EXISTS {projectName}_assets (asset_id integer primary key autoincrement, data blob not null, tag text not null, data_id integer, foreign key(data_id) references {projectName}(data_id))";
                string insertAssetQuery         = $"INSERT INTO {projectName}_assets(data, tag, data_id) values ($data, $tag, $data_id)";

                var status = executeCreateQuery(createTableLayoutQuery, DBConnection);
                // ERROR CHECKING REQUIRED

                status = executeCreateQuery(createProjectTableQuery, DBConnection);
                // ERROR CHECKING REQUIRED

                status = executeCreateQuery(createAssetTableQuery, DBConnection);
                // ERROR CHECKING REQUIRED

                // abort if any of the above fail

                var command = DBConnection.CreateCommand();
                command.CommandText = getTableLayoutQuery;
                command.Parameters.AddWithValue("$projectName", projectName);
                var reader = command.ExecuteReader();

                // check if table layout differs for this insert. If it does, error out.
                if (!InputParameterCheck(solution, projectName, DBConnection))
                {
                    throw new Exception("Input Parameters differ.");
                }

                bool hasErrored = false;
                using (var transaction = DBConnection.BeginTransaction(IsolationLevel.Serializable))
                {
                    // insert solution first
                    command = new SQLiteCommand(insertSolutionQuery, DBConnection, transaction);
                    command.Parameters.AddWithValue("$projectName", projectName);
                    command.Parameters.AddWithValue("$data", JsonConvert.SerializeObject(serializableSolution));
                    status = command.ExecuteNonQuery();
                    // ERROR CHECKING REQUIRED; write results to hasErrored

                    // get solution id
                    // for correctness' sake, get the id of the same object that was inserted
                    command = new SQLiteCommand(getSolutionIdQuery, DBConnection, transaction);
                    command.Parameters.AddWithValue("$projectName", projectName);
                    command.Parameters.AddWithValue("$data", JsonConvert.SerializeObject(serializableSolution));
                    Int64 solutionId = (Int64)command.ExecuteScalar();
                    // ERROR CHECKING REQUIRED; write results to hasErrored

                    // insert assets for the particular solution
                    foreach (KeyValuePair<string, string> asset in solution.files)
                    {

                        // TODO terminate this if solution insertion fails
                        byte[] fileContents = File.ReadAllBytes(asset.Key);

                        command = new SQLiteCommand(insertAssetQuery, DBConnection, transaction);
                        command.Parameters.AddWithValue("$projectName", projectName);
                        command.Parameters.AddWithValue("$data", fileContents);
                        command.Parameters.AddWithValue("$tag", asset.Key);
                        command.Parameters.AddWithValue("$data_id", solutionId);
                        status = command.ExecuteNonQuery();
                        // ERROR CHECKING REQUIRED; write results to hasErrored
                    }

                    if (hasErrored)
                    {
                        transaction.Rollback();
                    }
                    else
                    {
                        transaction.Commit();
                    }
                }

                // END Writing to Local DB

                // TODO abort csv insertion in case SQL insertion fails
                writeToCSV(directory, projectName, solution);
            }
            Console.WriteLine("database was closed by SaveToDisk.");

            DA.SetData(0, false);
            DA.SetData(0, true);
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