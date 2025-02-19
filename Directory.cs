using Grasshopper.Kernel;
using Newtonsoft.Json;
using System;
using System.IO;

namespace morpho
{

    public struct DirectoryParameters {
        public string directory;
        public string projectName;
    }

    /// <summary>
    /// The Directory component collects the location and project name under which the data should be saved.
    /// </summary>
    public class DirectoryComponent: GH_Component
    {

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public DirectoryComponent()
          : base("Directory", "Directory",
            "Location and Project Name under which the data should be saved",
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
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Output", "Output", "Should be connected to GeneGenerator and SaveToDisk", GH_ParamAccess.item);
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

            // Directory must exist and be writable
            if (!Directory.Exists(directory)) {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Folder does not exist.");
                return;
            }

            try {
                using (FileStream fs = File.Create(
                    Path.Combine(
                        directory,
                        Path.GetRandomFileName()
                    ),
                    1,
                    FileOptions.DeleteOnClose
                )) {
                }
            } catch {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Folder cannot be written to.");
            }

            DA.SetData(0, JsonConvert.SerializeObject(new DirectoryParameters{
                directory = directory,
                projectName = projectName,
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
        public override Guid ComponentGuid => new Guid("37787567-b748-46a6-9321-841964d13ba5");
    }
}