using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Data.SQLite;
using Grasshopper.Kernel;
using Newtonsoft.Json;
using Microsoft.VisualBasic;

namespace ghplugin
{

    public class SaveToDisk : GH_Component
    {

        public struct SerializableSolution
        {
            public Dictionary<string, double> input_parameters;
            public Dictionary<string, double> output_parameters;
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
            pManager.AddTextParameter("Directory", "Directory", "Directory to save the data under.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Enabled", "Enabled", "Enables the Component", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
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

        private enum ParameterCheckResult {
            NoProject,
            Valid,
            Invalid,
        };

        private ParameterCheckResult InputParameterCheck(MorphoAggregatedData solution, DirectoryParameters directory) {
            var db = new DBOps(directory);
            db.SetupDB();
            try {
                var inputParams = db.GetInputParameters(directory.projectName);
                foreach (var inputParameterPair in solution.inputs) {
                    if (!inputParams.ContainsKey(inputParameterPair.Key)) {
                        return ParameterCheckResult.Invalid;
                    }
                }
                return ParameterCheckResult.Valid;
            } catch (DBOps.ProjectNotFoundException) {
                return ParameterCheckResult.NoProject;
            }
        }

        protected static void checkError(bool success, string errorMessage)
        {
            if (!success)
                throw new Exception(errorMessage);
        }

        protected static void checkParameterError(bool success) {
            if (!success)
                throw new ParameterException();
        }

        protected static T GetParameter<T>(IGH_DataAccess DA, int position)
        {
            T data_item = default;
            checkParameterError(DA.GetData(position, ref data_item));
            return data_item;
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            try {
                string aggDataJson = GetParameter<string>(DA, 0);
                string directoryString = GetParameter<string>(DA, 1);
                bool enabled = GetParameter<bool>(DA, 2);

                var directoryObject = JsonConvert.DeserializeObject<DirectoryParameters>(directoryString);

                var projectName = System.Security.SecurityElement.Escape(directoryObject.projectName);

                if (!enabled) {
                    return;
                }

                // check if the directory is valid
                if (!Directory.Exists(directoryObject.directory))
                {
                    throw new Exception("Directory does not exist.");
                }

                MorphoAggregatedData solution = JsonConvert.DeserializeObject<MorphoAggregatedData>(aggDataJson);
                if (solution.inputs == null || solution.outputs == null)
                {
                    throw new ParameterException();
                }

                SerializableSolution serializableSolution = new SerializableSolution{ input_parameters = solution.inputs, output_parameters = solution.outputs };
                if (solution.inputs == null || solution.outputs == null)
                {
                    // if anything turns null, return without failing
                    return;
                }

                DBOps db = new DBOps(directoryObject);
                
                // check if input parameters differ for this insert. If it does, error out.
                var inputCheckResult = InputParameterCheck(solution, directoryObject);
                if (inputCheckResult == ParameterCheckResult.Invalid) {
                    throw new Exception("Input parameters do not match initial setup.");
                } else if (inputCheckResult == ParameterCheckResult.NoProject) {
                    db.InsertTableLayout(serializableSolution, solution.files, projectName);
                }

                // insert solution at this point, along with asset files / images
                // TODO implement inserting file paths
                db.InsertSolution(serializableSolution, solution.files, directoryObject.projectName);

                // TODO implement writeToCSV() for the new setup
                // writeToCSV(directoryObject.directory, projectName, solution);
            } catch (ParameterException) {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Missing Parameters");
            } catch (DBOps.InsertionError e) {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
            } catch (DBOps.ProjectNotFoundException e) {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
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
        public override Guid ComponentGuid => new Guid("3c6102ac-8a0e-49fe-b82a-2b09f0b2acf6");
    }
}