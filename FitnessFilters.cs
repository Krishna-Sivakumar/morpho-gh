using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace ghplugin
{
    
    public class Filter : GH_Component
    {
        public static string Join(List<string> strings, string delimiter) {
            var result = new StringBuilder();
            for (int i = 0; i < strings.Count; i ++) {
                if (i == strings.Count - 1) {
                    result.Append(strings[i]);
                } else {
                    result.Append(strings[i]);
                    result.Append(delimiter);
                }
            }
            return result.ToString();
        }

        public static string Join(string[] strings, string delimiter) {
            var result = new StringBuilder();
            for (int i = 0; i < strings.Length; i ++) {
                if (i == strings.Length - 1) {
                    result.Append(strings[i]);
                } else {
                    result.Append(strings[i]);
                    result.Append(delimiter);
                }
            }
            return result.ToString();
        }

        private const string greaterThan = ">";
        private const string lesserThan = "<";
        private const string equalTo = "=";
        private const string lesserThanOrEqualTo = "<=";
        private const string greaterThanOrEqualTo = ">=";
        private string[] operatorSet = { greaterThan, lesserThan, equalTo, lesserThanOrEqualTo, greaterThanOrEqualTo };

        string filterOperator;
        string parameterName;
        string filterValue;

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public Filter()
          : base("Filter", "Filter",
            "Determines an individual filter for the fitness function",
            "Morpho", "Conditional Filters")
        {
            this.filterOperator = greaterThan;
            this.NickName = this.filterOperator;
        }

        public override void
        AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            foreach (string op in operatorSet) {
                Menu_AppendItem(menu, op, delegate (Object o, EventArgs e) {
                    this.filterOperator = op;
                    this.NickName = this.filterOperator;
                    this.ExpireSolution(true);
                });
            }
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

            pManager.AddTextParameter("pName", "Parameter Name", "Name of the parameter to be filtered.", GH_ParamAccess.item);
            pManager.AddTextParameter("pVal", "Parameter Value", "Value to filter by", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("filter", "filter", "Definition of a filter", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            DA.GetData(0, ref this.parameterName);
            DA.GetData(1, ref this.filterValue);
            var expression = $"json_extract(data, '$.parameters.{this.parameterName}') {this.filterOperator} {this.filterValue}";
            DA.SetData(0, expression);
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
        public override Guid ComponentGuid => new Guid("404dcd19-39a4-44ef-8a27-caa51f55a854");
    }

    public class FilterConjuction : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public FilterConjuction()
          : base("Filter Conjunction", "Filter Conjuction",
            "Joins two or more fitness filters with a binary AND",
            "Morpho", "Conditional Filters")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Fitness Filters", "Fitness Filters", "Set of fitness filters to be joined together.", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("filter", "filter", "Definition of a filter", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
         
           List<string> fitnessFilters = new List<string>();
           DA.GetDataList(0, fitnessFilters);
           var expression = Filter.Join(fitnessFilters, " AND ");
           DA.SetData(0, expression);
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
        public override Guid ComponentGuid => new Guid("dfe929fd-53ca-4d77-9258-45829841306c");
    }

    public class FilterDisjunction : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public FilterDisjunction()
          : base("Filter Disjunction", "Filter Disjunction",
            "Joins two or more fitness filters with a boolean OR",
            "Morpho", "Conditional Filters")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Fitness Filters", "Fitness Filters", "Set of fitness filters to be joined together.", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("filter", "filter", "Definition of a filter", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
           List<string> fitnessFilters = new List<string>();
           DA.GetDataList(0, fitnessFilters);
           var expression = Filter.Join(fitnessFilters.ToArray(), " OR ");
           DA.SetData(0, expression);
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
        public override Guid ComponentGuid => new Guid("54666c2d-5c2f-48ef-8921-5a33cbfadd99");
    }
}