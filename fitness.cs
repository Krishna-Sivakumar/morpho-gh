using Grasshopper.Kernel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace ghplugin
{

    public class Fitness: GH_Component, MorphoBase
    {

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

        protected static void checkError(bool success, string errorMessage) {
            if (!success)
                throw new Exception(errorMessage);
        }

        protected static void checkError(bool success) {
            checkError(success, "parameters missing.");
        }

        protected static T GetParameter<T>(IGH_DataAccess DA, int position) {
            T data_item = default;
            checkError(DA.GetData(position, ref data_item));
            return data_item;
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string directory = GetParameter<string>(DA, 0);
            string projectName = GetParameter<string>(DA, 1);
            string fitnessConditions = GetParameter<string>(DA, 2); // TODO fitness condition should be its own set of component

            DA.SetData(0, JsonConvert.SerializeObject(new GeneGenerator.SolutionSetParameters{
                directory = directory,
                projectName = projectName,
                fitnessConditions = fitnessConditions
            }));
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