using System;
using System.Timers;
using Newtonsoft.Json;

using Grasshopper.Kernel;
using Rhino;

namespace ghplugin
{

  public struct MorphoInterval
  {
    public string name;
    public double start;
    public double end;
    public bool is_constant;
  }
  public class Interval : GH_Component
  {

    public MorphoInterval interval;

    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public Interval()
      : base("Interval", "interval",
        "Creates an interval from numerical inputs",
        "Morpho", "Genetic Search")
    {
      interval = new MorphoInterval{start = 0, end = 0, is_constant = false};
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
      pManager.AddNumberParameter("Start", "start", "Start of Interval", GH_ParamAccess.item);
      pManager.AddNumberParameter("End", "end", "End of Interval", GH_ParamAccess.item);
      Params.Input[1].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
      pManager.AddTextParameter("Interval", "interval", "Interval generated", GH_ParamAccess.item);
    }

    private Timer debounce;

    private void expireSolution(object sender, object args) {
      // we do not want to call ExpireSolution too many times on a nickname change. So we debounce the input by a second.
      if (debounce == null) {
        debounce = new Timer(1000);
        debounce.AutoReset = false;
        debounce.Elapsed += (object s, ElapsedEventArgs e) => {
          // Cannot call ExpireSolution(true) directly if it's not a UI component. So we do this.
          // Refer to https://discourse.mcneel.com/t/system-invalidoperationexception-cross-thread-operation-not-valid/95176.
          var d = new ExpireSolutionDelegate(ExpireSolution);
          RhinoApp.InvokeOnUiThread(d, true);
          debounce.Stop();
          debounce = null;
        };
      } else {
        debounce.Stop();
        debounce.Start();
      }
    }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      // needed to respond to nickname changes
      this.ObjectChanged += expireSolution;

      checkError(DA.GetData(0, ref interval.start));
      interval.name = NickName;
      interval.is_constant = !DA.GetData(1, ref interval.end);

      if (interval.start > interval.end) {
        interval.end = interval.start;
      }

      DA.SetData(0, JsonConvert.SerializeObject(interval));
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
    public override Guid ComponentGuid => new Guid("2d55cacf-9a72-46d9-b0cc-a46ad1081145");
  }
}