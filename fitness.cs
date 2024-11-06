using Grasshopper.Kernel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace ghplugin
{

    public class Fitness : GH_Component
    {
        private SQLiteConnection DBConnection;

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
            "Filters out solutions from the global solution set",
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
            pManager.AddTextParameter("Directory", "Directory", "Directory where the data should be saved into.", GH_ParamAccess.item);
            pManager.AddTextParameter("Project Name", "Project Name", "Name of the project.", GH_ParamAccess.item);
            pManager.AddTextParameter("Fitness Conditions", "Fitness Conditions", "Conditions imposed on the solution set", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Solution Set", "Solution Set", "Solutions filtered out from the set using the provided conditions", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string directory = "";
            DA.GetData(0, ref directory);
            string projectName = "";
            DA.GetData(1, ref projectName);
            string fitnessConditions = "";
            DA.GetData(2, ref fitnessConditions); // TODO fitness condition should be its own set of component

            List<Dictionary<string, double>> solutions = new List<Dictionary<string, double>>();

            // 1. make a connection to directory/solutions.db
            using (DBConnection = new SQLiteConnection($"Data Source={directory}/solutions.db"))
            {
                DBConnection.Open();

                // 2. form the query
                var query = "";
                if (fitnessConditions.Trim().Length > 0)
                {
                    query = $"SELECT data FROM {projectName} WHERE {fitnessConditions};";
                }
                else
                {
                    // if the fitness function is empty, select everything
                    query = $"SELECT data FROM {projectName};";
                }

                // 3. make the query
                var command = DBConnection.CreateCommand();
                command.CommandText = query;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // 4. deserialize and store the results in an array
                        var json_object = reader.GetString(0);
                        solutions.Add(JsonConvert.DeserializeObject<MorphoAggregatedData>(json_object).inputs);
                    }
                }
                // 5. serialize the array to json and write it to the output parameter
                var serialized_output = JsonConvert.SerializeObject(solutions);
                DA.SetData(0, serialized_output);
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
        public override Guid ComponentGuid => new Guid("296b073b-0e8c-47a0-970a-04f447df42ce");
    }
}