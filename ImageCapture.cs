using Grasshopper.Kernel;
using Rhino;
using Rhino.Display;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace morpho
{

    public struct ViewportDetails
    {
        public RhinoView viewport;
        public string name;
        public string path;

        public override string ToString()
        {
            return $"ImageCapture: {viewport} {name}";
        }

        /// <summary>
        /// Captures an image from either the viewport provided, or the path specified.
        /// This method can fail in 3 ways:
        /// 1. No viewport or path provided
        /// 2. Can't convert the path to an image
        /// 3. Can't capture an image from the viewport.
        /// </summary>
        /// <returns>A Bitmap captured from the viewport, or the path specified.</returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="OutOfMemoryException">If the given path is not an image, this exception is thrown.</exception>
        public Bitmap GetImage()
        {
            if (viewport != null)
            {
                return viewport.CaptureToBitmap();
            }
            else if (path != "")
            {
                return new Bitmap(Image.FromFile(path));
            }
            else
            {
                throw new Exception("No path or viewport is set.");
            }
        }

        public bool IsValid()
        {
            return viewport != null || File.Exists(path);
        }
    }

    /// <summary> K </summary>
    public struct NamedBitmap
    {
        /// <summary> Enclosed Bitmap object </summary>
        public Bitmap bitmap;
        /// <summary> Name of the Bitmap object </summary>
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
            "Selects a viewport from the active Rhino3D document or an image file locally and captures it into a bitmap image. If the path is set, it takes precedence over the viewport.",
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
            pManager.AddTextParameter("File", "File", "Optional file path to read the image from", GH_ParamAccess.item);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Image", "Image", "Bitmap Image of the Viewport", GH_ParamAccess.item);
        }

        /// <summary> Throws an exception with a message if the success flag is set to false.</summary>
        /// <param name="success">flag to check.</param>
        /// <param name="errorMessage">message to be included in the exception.</param>
        /// <exception cref="Exception"></exception>
        protected static void checkError(bool success, string errorMessage)
        {
            if (!success)
                throw new Exception(errorMessage);
        }

        /// <summary> Throws an exception with a message if the success flag is set to false.</summary>
        /// <param name="success">flag to check.</param>
        protected static void checkError(bool success)
        {
            checkError(success, "parameters missing.");
        }

        /// <summary>Gets an input parameter typed with T. If the parameter is not present, throws an exception.</summary>
        /// <param name="DA">Component Data Access object.</param>
        /// <param name="name">Name of the parameter to get.</param>
        /// <returns>Value of the parameter, typed with T.</returns>
        /// <exception cref="Exception"></exception>
        protected static T GetParameter<T>(IGH_DataAccess DA, string name)
        {
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
            // Either get the file, or the viewport. Both can't be null.
            var tag = GetParameter<string>(DA, "Tag");
            bool fileFound = true;
            try
            {
                var filePath = GetParameter<string>(DA, "File");
                ViewportDetails pathVp = new ViewportDetails
                {
                    viewport = null,
                    name = tag,
                    path = filePath,
                };
                DA.SetData(0, pathVp);
                return;
            }
            catch (Exception)
            {
                // do nothing in case the file path is not set.
                fileFound = false;
            }

            if (viewport == null && fileFound == false)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Neither Viewport nor Filepath is present. Use the component context menu to set the viewport.");
                return;
            }

            ViewportDetails vp = new ViewportDetails
            {
                viewport = viewport,
                name = tag,
                path = "",
            };

            DA.SetData(0, vp);
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
        protected override Bitmap Icon {
            get {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                if (false) {
                    // use this when you need to list out the names of embedded resources
                    string[] result = assembly.GetManifestResourceNames();
                    Console.WriteLine("manifest resources:");
                    foreach (var res in result) {
                        Console.WriteLine(res);
                    }
                }
                var stream = assembly.GetManifestResourceStream("ghplugin.icons.image_capture.png");
                return new Bitmap(stream);
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("187d673b-d544-4056-ab3d-dab31da704ee");
    }
}
