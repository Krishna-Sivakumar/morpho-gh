using Grasshopper.Kernel;
using Rhino;
using Rhino.Display;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace morpho
{

    public struct NamedBitmap {
        public Bitmap bitmap;
        public string name;
    }

    /// <summary>
    /// The Directory component collects the location and project name under which the data should be saved.
    /// </summary>
    public class ImageCapture: GH_Component
    {

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public ImageCapture()
          : base("Image Capture", "Image Capture",
            "Selects a viewport from the activate Rhino3D document and captures it a bitmap image.",
            "Morpho", "Genetic Search")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Use the pManager object to register your input parameters.
            // You can often supply default values when creating parameters.
            // All parameters must have the correct access type. If you want 
            // to import lists or trees of values, modify the ParamAccess flag.
            pManager.AddTextParameter("Tag", "Tag", "Tag for the captured viewport", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Image", "Image", "Bitmap Image of the Viewport", GH_ParamAccess.item);
        }

        protected static void checkError(bool success, string errorMessage) {
            if (!success)
                throw new Exception(errorMessage);
        }

        protected static void checkError(bool success) {
            checkError(success, "parameters missing.");
        }

        protected static T GetParameter<T>(IGH_DataAccess DA, string name) {
            T data_item = default;
            checkError(DA.GetData(name, ref data_item));
            return data_item;
        }

        // Setting up Menu Items for selecting the viewport

        private RhinoView viewport;
        private Dictionary<string, Guid>  viewportMap;

        protected void MenuClickHandler(object sender, EventArgs e) {
            // event handler to be called when the toolstrip buttons are clicked
            var button = (ToolStripButton) sender;
            viewport = RhinoDoc.ActiveDoc.Views.Find(viewportMap[button.Text]);

            // Cannot call ExpireSolution(true) directly if it's not a UI component. So we do this.
            // Refer to https://discourse.mcneel.com/t/system-invalidoperationexception-cross-thread-operation-not-valid/95176.
            var d = new ExpireSolutionDelegate(ExpireSolution);
            RhinoApp.InvokeOnUiThread(d, true);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu) {
            viewportMap = new Dictionary<string, Guid>();
            List<ToolStripButton> buttons  = new List<ToolStripButton>();
            foreach (var view in RhinoDoc.ActiveDoc.Views) {
                var tempButton = new ToolStripButton();
                // TODO Active viewport might not be what we need.
                tempButton.Text = view.ActiveViewport.Name;
                tempButton.Click += MenuClickHandler;
                buttons.Add(tempButton);
                viewportMap.Add(view.ActiveViewport.Name, view.ActiveViewportID);
            }

            // TODO support named views later

            var label = new ToolStripLabel();
            label.Text = "Select Viewport:";
            menu.Items.Add(label);
            menu.Items.AddRange(buttons.ToArray());
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var tag = GetParameter<string>(DA, "Tag");

            if (this.viewport == null) {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Viewport not set. Use the component context menu to set it.");
                return;
            }

            var captureSettings = new ViewCaptureSettings(this.viewport, this.viewport.Size, 1200);
            captureSettings.Resolution = 300;
            captureSettings.Document = RhinoDoc.ActiveDoc;
            captureSettings.DrawAxis = false;
            captureSettings.DrawAxis = false;
            captureSettings.DrawGrid = false;
            captureSettings.RasterMode = true;
            captureSettings.DrawBackground = false;
            captureSettings.SetModelScaleToFit(true);

            NamedBitmap namedBitmap = new NamedBitmap{
                bitmap =  ViewCapture.CaptureToBitmap(captureSettings),
                name = tag
            };

            DA.SetData(0, namedBitmap);
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
        protected override Bitmap Icon => null;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("187d673b-d544-4056-ab3d-dab31da704ee");
    }
}