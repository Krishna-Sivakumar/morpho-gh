using System;
using System.Collections.Generic;
using Newtonsoft.Json;

using Grasshopper;
using Grasshopper.Kernel;
using Rhino.ApplicationSettings;
using Rhino.Geometry;
using Rhino.Commands;
using Rhino.Runtime.RhinoAccounts;
using System.Windows.Forms;
using Grasshopper.Kernel.Parameters;
using System.Windows.Markup;
using System.Security.Cryptography;
using System.Xml.Serialization;
using System.Diagnostics;
using System.IO;

namespace ghplugin
{


    public class DataTest : GH_Component
    {
        private int counter;

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public DataTest()
          : base("DataTest", "Data Test", "", "Morpho", "")
        {
            counter = 0;
        }

        private void checkError(bool success)
        {
            if (!success)
                throw new Exception("Parameters Missing.");
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
            pManager.AddBooleanParameter("Start", "start", "Start of Interval", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Output", "output", "Output of the component", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool is_active = false;
            if (!DA.GetData(0, ref is_active))
            {
                return;
            }
            if (is_active)
            {
                counter++;
                DA.SetData(0, counter);
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
        public override Guid ComponentGuid => new Guid("eb2c28a8-8b90-433d-b0a5-857ffcd7cf2d");
    }
}