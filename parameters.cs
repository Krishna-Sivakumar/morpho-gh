using System;
using System.Collections.Generic;
using Newtonsoft.Json;

using Grasshopper;
using Grasshopper.Kernel;
using Rhino.ApplicationSettings;
using Rhino.Geometry;
using Rhino.Commands;
using Rhino.Runtime.RhinoAccounts;

struct ParameterSet
{
  public double probability_xover;
  public double probability_mutation;
  public double dist_index_xover;
  public double dist_index_mutation;

  public void Default()
  {
    // need to change this later
    probability_xover = 0.5;
    probability_mutation = 0.5;
    dist_index_xover = 0;
    dist_index_mutation = 0;
  }
}

namespace ghplugin
{
  public class Parameters : GH_Component
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public Parameters()
      : base("Parameters", "params",
        "Parameters for the Genetic Search",
        "Morpho", "Genetic Search")
    {
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
      pManager.AddNumberParameter("Cross-Over Probability", "p(xover)", "Cross-Over Probability used by the algorithm", GH_ParamAccess.item);
      pManager.AddNumberParameter("Mutation Probability", "p(mut)", "Mutation Probability used by the algorithm", GH_ParamAccess.item);
      pManager.AddNumberParameter("Cross-Over Distribution Index", "xover_dist", "", GH_ParamAccess.item);
      pManager.AddNumberParameter("Mutation Distribution Index", "mut_dist", "", GH_ParamAccess.item);
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
      // Use the pManager object to register your output parameters.
      // Output parameters do not have default values, but they too must have the correct access type.
      pManager.AddTextParameter("Result", "r", "Set of Parameters, Encoded", GH_ParamAccess.item);

      // Sometimes you want to hide a specific parameter from the Rhino preview.
      // You can use the HideParameter() method as a quick way:
      //pManager.HideParameter(0);
    }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      ParameterSet p = new ParameterSet();

      checkError(DA.GetData("Cross-Over Probability", ref p.probability_xover));
      checkError(DA.GetData("Mutation Probability", ref p.probability_mutation));
      checkError(DA.GetData("Cross-Over Distribution Index", ref p.dist_index_xover));
      checkError(DA.GetData("Mutation Distribution Index", ref p.dist_index_mutation));

      string json = JsonConvert.SerializeObject(p);
      DA.SetData(0, json);
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
    public override Guid ComponentGuid => new Guid("e0e098e6-0f5d-49f3-8eae-b6285862bdf3");
  }
}