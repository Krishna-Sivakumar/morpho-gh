using System.Data.SQLite;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using System.Data;
using System.IO;
using System.Linq;

namespace morpho {

    /// <summary>
    /// Represents a metadata field within a project.
    /// </summary>
    public struct MetadataField {
        public string field_name, field_unit, field_type;
    }

    public struct AssetField {
        public string description, extension, mimeType, tag;
    }

    public class DBOps {
        DirectoryParameters directoryParameters;
        SQLiteConnectionStringBuilder connectionBuilder;

        public DBOps(DirectoryParameters d) {
            this.directoryParameters = d;
            SetupDB();
        }

        private int executeCreateQuery(string query, SQLiteConnection DBConnection)
        {
            var command = DBConnection.CreateCommand();
            command.CommandText = query;
            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// Sets up the local database with the initial tables needed.
        /// </summary>
        public void SetupDB() {
            var connBuilder = new SQLiteConnectionStringBuilder();
            connBuilder.DataSource = $"{directoryParameters.directory}/solutions.db";
            connBuilder.Version = 3;
            connBuilder.JournalMode = SQLiteJournalModeEnum.Wal;
            connBuilder.LegacyFormat = false;
            connBuilder.Pooling = true;

            connectionBuilder = connBuilder;

            using (var DBConnection = new SQLiteConnection(connectionBuilder.ToString()))
            {
                DBConnection.Open();

                string createAssetTable = $"CREATE TABLE IF NOT EXISTS asset (id text primary key, file text, tag text, solution_id text, foreign key(solution_id) references solution(id))";
                string createMetadataTable = $"CREATE TABLE IF NOT EXISTS metadata (project_name text primary key, captions jsonb, human_name text, slug text, text text, foreign key(project_name) references project(project_name))";
                string createProjectTable = $"CREATE TABLE IF NOT EXISTS project (creation_date date not null, project_name text primary key, variable_metadata jsonb not null, output_metadata jsonb not null, assets jsonb, deleted integer not null)";
                string createSolutionTable = $"CREATE TABLE IF NOT EXISTS solution (id text primary key, parameters jsonb not null, output_parameters jsonb, project_name text not null, scoped_id integer, foreign key(project_name) references project(project_name))";

                var status = executeCreateQuery(createAssetTable, DBConnection);
                status = executeCreateQuery(createMetadataTable, DBConnection);
                status = executeCreateQuery(createProjectTable, DBConnection);
                status = executeCreateQuery(createSolutionTable, DBConnection);

                DBConnection.Close();
            }
        }

        [Serializable]
        public class ProjectNotFoundException: Exception {
            public ProjectNotFoundException(): base() {}
            public ProjectNotFoundException(string message) : base (message) {}
            public ProjectNotFoundException(string message, Exception innerException) : base (message, innerException) {}
        }

        /// <summary>
        /// Gets the metadata fields for the input parameter section of a project.
        /// </summary>
        /// <param name="projectName">Name of the project.</param>
        /// <returns>Collection of input metadata fields associated with the project.</returns>
        /// <exception cref="ProjectNotFoundException">Thrown when a project with the name could not be found.</exception>
        public Dictionary<string, MetadataField> GetInputParameters(string projectName) {
            string query = "SELECT variable_metadata FROM project WHERE project_name = $projectName";
            using (var DBConnection = new SQLiteConnection(connectionBuilder.ToString())) {
                DBConnection.Open();
                var command = DBConnection.CreateCommand();
                command.CommandText = query;
                command.Parameters.AddWithValue("$projectName", projectName);
                using (var reader = command.ExecuteReader()) {
                    if (reader.Read()) {
                        string metadata = reader.GetString(0);
                        var fields = JsonConvert.DeserializeObject<MetadataField[]>(metadata);
                        var result = new Dictionary<string, MetadataField>();
                        foreach (var field in fields) {
                            result.Add(field.field_name, field);
                        }
                        DBConnection.Close();
                        return result;
                    } else {
                        DBConnection.Close();
                        throw new ProjectNotFoundException($"Could not find project {projectName}");
                    }
                }
            }
        }

        private struct MetadataPair {
            public MetadataField[] input, output;
            public AssetField[] assets;
        }

        /// <summary>
        /// Takes a solution and returns what its metadata fields could look like.
        /// </summary>
        /// <param name="solution">The solution to be processed.</param>
        /// <param name="assets">Pairs of (tag, filepath) to be processed.</param>
        /// <returns>Input and Output MetadataField collections as a tuple, in that order.</returns>
        private MetadataPair SerializeSolutionToMetadata(SaveToPopulation.SerializableSolution solution, Dictionary<string, string> assets, string[] images) {
            List<MetadataField> input = new List<MetadataField>();
            List<MetadataField> output = new List<MetadataField>();
            List<AssetField> assetFields = new List<AssetField>();

            foreach (KeyValuePair<string, double> inputPair in solution.input_parameters) {
                input.Add(new MetadataField{field_name = inputPair.Key, field_type = "DOUBLE", field_unit = ""});
            }
            foreach (KeyValuePair<string, double> outputPair in solution.output_parameters) {
                output.Add(new MetadataField{field_name = outputPair.Key, field_type = "DOUBLE", field_unit = ""});
            }

            Mime m = new Mime();
            foreach (string tagName in images) {
                var mimetype = m.GetMimeType(".png");
                assetFields.Add(new AssetField{description = "", extension = ".png", tag = tagName, mimeType = mimetype});
            }

            foreach (KeyValuePair<string, string> assetPair in assets) {
                // Fill in whatever mime type we know, otherwise rely on external code to set it
                var extension = Path.GetExtension(assetPair.Value);
                var mimetype = m.GetMimeType(extension);
                assetFields.Add(new AssetField{description = "", extension = extension.Substring(1, extension.Length - 1), tag = assetPair.Key, mimeType = mimetype});
            }

            return new MetadataPair {
                input = input.ToArray(),
                output = output.ToArray(),
                assets = assetFields.ToArray(),
            };
        }

        /// <summary>
        ///  Inserts a table layout into the local database.
        /// </summary>
        /// <returns>The number of rows inserted; should be 1.</returns>
        public int InsertTableLayout(SaveToPopulation.SerializableSolution solution, Dictionary<string, string> assets, string[] images, string projectName) {
            string query = "INSERT INTO project(creation_date, project_name, variable_metadata, output_metadata, assets, deleted) VALUES ($creationDate, $projectName, $variableMetadata, $outputMetadata, $assets, False)";
            string metadataInsertQuery = "INSERT INTO metadata(project_name, captions, human_name, slug, text) VALUES ($projectName, $captions, $humanName, $slug, $text)";
            var creationDate = DateTime.Today;
            var metadataTuple = SerializeSolutionToMetadata(solution, assets, images);

            using (var connection = new SQLiteConnection(connectionBuilder.ToString())) {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.AddWithValue("$creationDate", creationDate);
                command.Parameters.AddWithValue("$projectName", projectName);
                command.Parameters.AddWithValue("$variableMetadata", JsonConvert.SerializeObject(metadataTuple.input));
                command.Parameters.AddWithValue("$outputMetadata", JsonConvert.SerializeObject(metadataTuple.output));
                command.Parameters.AddWithValue("$assets", JsonConvert.SerializeObject(metadataTuple.assets));
                var status = command.ExecuteNonQuery();

                command = connection.CreateCommand();
                command.CommandText = metadataInsertQuery;
                command.Parameters.AddWithValue("$projectName", projectName);
                command.Parameters.AddWithValue("$captions", "[]");
                command.Parameters.AddWithValue("$humanName", "");
                command.Parameters.AddWithValue("$slug", "");
                command.Parameters.AddWithValue("$text", "");
                status = command.ExecuteNonQuery();

                connection.Close();
                return status;
            }
        }

        [Serializable]
        public class InsertionError: Exception {
            public InsertionError(): base() {}
            public InsertionError(string message) : base (message) {}
            public InsertionError(string message, Exception innerException) : base (message, innerException) {}
        }
        public string uuidv4() {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Inserts a solution into the local database.
        /// </summary>
        /// <param name="solution">The serializable solution to be inserted</param>
        /// <returns>Number of rows inserted; should be 1.</returns>
        public (string, string) InsertSolution(SaveToPopulation.SerializableSolution solution, string projectName) {
            // should insert assets as well, in a transaction
            string insertedSolutionId = "";
            string insertedSolutionScopedId = "";
            string insertSolution = """
                INSERT INTO solution(id, parameters, output_parameters, project_name, scoped_id)
                VALUES ($id, $parameters, $outputParameters, $projectName, ( SELECT ifnull(max(scoped_id),0) + 1 FROM solution WHERE project_name = $projectName )
                RETURNING id, scoped_id;
            """;
            using (var connection = new SQLiteConnection(connectionBuilder.ToString())) {
                // begin a transaction here
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        // insert solution first
                        var command = connection.CreateCommand();
                        command.CommandText = insertSolution;
                        command.Parameters.AddWithValue("$parameters", JsonConvert.SerializeObject(solution.input_parameters));
                        command.Parameters.AddWithValue("$outputParameters", JsonConvert.SerializeObject(solution.output_parameters));
                        command.Parameters.AddWithValue("$projectName", projectName);
                        command.Parameters.AddWithValue("$id", uuidv4());
                        string solutionId;
                        string scopedId;
                        try
                        {
                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    solutionId = reader.GetString(0);
                                    scopedId = reader.GetString(1);
                                }
                                else
                                {
                                    throw new InsertionError("Could not insert solution into the database.");
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            throw new InsertionError($"Could not insert solution into the database: {e.Message}");
                        }

                        transaction.Commit();
                        insertedSolutionId = solutionId;
                        insertedSolutionScopedId = scopedId;
                    }
                    catch (InsertionError e)
                    {
                        transaction.Rollback();
                        throw new InsertionError(e.Message);
                    }
                }
                connection.Close();
            }
            return (insertedSolutionId, insertedSolutionScopedId);
        }

        /// <summary>
        /// Inserts a solution's assets into the local database.
        /// </summary>
        /// <param name="solution">The serializable solution to be inserted</param>
        /// <returns>Number of rows inserted; should be 1.</returns>
        public long InsertSolutionAssets(string solutionId, Dictionary<string, string> assets) {
            // should insert assets as well, in a transaction
            string insertAsset = "INSERT INTO asset(file, tag, solution_id) VALUES ($file, $tag, $solutionId)";
            using (var connection = new SQLiteConnection(connectionBuilder.ToString())) {
                // begin a transaction here
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable)) {
                    try {
                        // insert every asset
                        var command = connection.CreateCommand();
                        foreach (KeyValuePair<string, string> asset in assets) {
                            command = connection.CreateCommand();
                            command.CommandText = insertAsset;
                            command.Parameters.AddWithValue("$tag", asset.Key);
                            command.Parameters.AddWithValue("$file", asset.Value);
                            command.Parameters.AddWithValue("$solutionId", solutionId);
                            var result = command.ExecuteNonQuery();
                        }

                        // commit if everything goes well.
                        transaction.Commit();
                    } catch (InsertionError e) {
                        transaction.Rollback();
                        throw new InsertionError(e.Message);
                    }
                }
                connection.Close();
            }
            int status = 0;
            return status;
        }

        /// <summary>
        /// Gets a mapping from parameter to category of parameter (input / output)
        /// </summary>
        private Dictionary<string, ParamType> GetSchema(string projectName) {
            // TODO stub; fill in later
            var schema = new Dictionary<string, ParamType>();

            using (var DBConnection = new SQLiteConnection(connectionBuilder.ToString()))
            {
                DBConnection.Open();

                var query = "SELECT parameters, output_parameters FROM solution WHERE project_name=$projectName ORDER BY id DESC LIMIT 1";
                var command = DBConnection.CreateCommand();
                command.CommandText = query;
                command.Parameters.AddWithValue("$projectName", projectName);
                using (var reader = command.ExecuteReader()) {
                    while (reader.Read()) {
                        // inspect the contents of the first 
                        Dictionary<string, double> parameters = JsonConvert.DeserializeObject<Dictionary<string, double>>(reader.GetString(0));
                        Dictionary<string, double> outputParameters = JsonConvert.DeserializeObject<Dictionary<string, double>>(reader.GetString(1));
                        foreach (var pair in parameters) {
                            schema.Add(pair.Key, ParamType.Input);
                        }
                        foreach (var pair in outputParameters) {
                            schema.Add(pair.Key, ParamType.Output);
                        }
                    }
                }
            }

            return schema;
        }

        /// <summary>
        /// Checks if a particular solution exists in the database.
        /// </summary>
        /// <param name="projectName">Name of the project to query against</param>
        /// <param name="input">The set of inputs variables to check the existence of</param>
        /// <returns></returns>
        public bool CheckIfSolutionExists(string projectName, Dictionary<string, double> input) {
            string[] conditions = input.Select(pair => $"json_extract(parameters, \'$.{pair.Key}\') = {pair.Value}").ToArray();
            var condition = string.Join(" AND ", conditions);
            var solutions = GetSolutions(projectName, condition);
            return solutions.Length > 0;
        }

        /// <summary>
        /// Gets a list of solution associated with a projected and constrained by a set of fitnessConditions.
        /// </summary>
        /// <param name="projectName">Name of the project to query against.</param>
        /// <param name="fitnessConditions">Fitness conditions to constrain solution by. Leave empty to get all solutions associated with the project.</param>
        /// <returns></returns>
        public MorphoSolution[] GetSolutions(string projectName, string fitnessConditions) {
            // if GetSchema() returns a dictionary with nothing i.e. there are no records yet, terminate the query and return nothing.
            List<MorphoSolution> solutions = new List<MorphoSolution>();
            var schema = GetSchema(projectName);
            if (schema.Count == 0)
            {
                return solutions.ToArray();
            }

            // 1. make a connection to directory/solutions.db
            using (var DBConnection = new SQLiteConnection(connectionBuilder.ToString()))
            {
                DBConnection.Open();

                // 2. form the query
                var query = "";
                if (fitnessConditions.Trim().Length > 0)
                {
                    query = $"SELECT parameters FROM solution WHERE project_name=$projectName AND {fitnessConditions};";
                }
                else
                {
                    // if the fitness function is empty, select everything
                    query = $"SELECT parameters FROM solution WHERE project_name=$projectName;";
                }

                // 3. get the set of input parameters
                var command = DBConnection.CreateCommand();

                // 3. make the query
                command = DBConnection.CreateCommand();
                command.CommandText = query;
                command.Parameters.AddWithValue("$projectName",projectName);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // 4. deserialize and store the results in an array
                        var jsonString = reader.GetString(0);
                        Dictionary<string, double> culledSolution = JsonConvert.DeserializeObject<Dictionary<string, double>>(jsonString);
                        solutions.Add(new MorphoSolution { values = culledSolution });
                    }
                }
                DBConnection.Close();
            }
            return solutions.ToArray();
        }

        /// <summary>
        /// Gets the number of solutions associated with a project
        /// </summary>
        /// <param name="projectName">Name of the project in the local database.</param>
        /// <returns>Number of solutions associated with a project. Returns 0 if there's no project created at that point.</returns>
        public long GetSolutionCount(string projectName) {
            using (var connection = new SQLiteConnection(connectionBuilder.ToString()))
            {
                connection.Open();
                string query = $"select COUNT(*) from solution WHERE project_name = $projectName";
                var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.AddWithValue("$projectName", projectName);
                var result = command.ExecuteScalar();
                connection.Close();
                if (result == null) {
                    return 0;
                } else {
                    return (long)result;
                }
            }
        }
    }
}
