using System;
using System.Collections.Generic;
using System.Windows.Forms;

using Grasshopper.Kernel;

namespace morpho
{
    /*
     * USAGE:
     * FilterExpression captures a nested set of fitness filters.
     * Calling Eval() on the top level filter will recursively resolve the expressions and return a string
     * ready to be inserted into the WHERE condition of a SQL query.
     *
     * However, the user will have to provide a $projectName variable during query execution
     * and a dictionary mapping parameters to their category (input or output variables) to the top-level Eval() call.
     *
     * The $projectName variable is necessary for executing subqueries for Top-N and Bottom-N selection,
     * and the category mapping is required to build the correct query without having to 
     * use column-agnostic queries like before.
    */

    public enum ParamType {
        Input,
        Output
    }

    public enum FilterJoinType {
        AND,
        OR
    }
    
    public enum FilterOpType {
        GreaterThan,
        LesserThan,
        Equal,
        NotEqual,
        GreaterThanOrEqual,
        LesserThanOrEqual,
        TopN,
        BottomN
    }

    static class FilterEnumResolution {
        public static string ResolveParamType(ParamType pt) {
            if (pt == ParamType.Input) {
                return "parameters";
            } else {
                return "output_parameters";
            }
        }

        public static string ResolveJoin(FilterJoinType jt) {
            if (jt == FilterJoinType.AND) {
                return "AND";
            } else {
                return "OR";
            }
        }

        public static string ResolveOp(FilterOpType op) {
            switch (op) {
                case FilterOpType.GreaterThan: return ">";
                case FilterOpType.LesserThan: return "<";
                case FilterOpType.Equal: return "=";
                case FilterOpType.NotEqual: return "<>";
                case FilterOpType.GreaterThanOrEqual: return ">=";
                case FilterOpType.LesserThanOrEqual: return "<=";
                case FilterOpType.TopN: return "TopN";
                case FilterOpType.BottomN: return "BottomN";
            }
            throw new Exception("Invalid Operation");
        }
    }

    

    public interface FilterExpression {
        string Eval(Dictionary<string, ParamType> paramTypes);
    }

    /// <summary> Returns an empty string. This is a placeholder. </summary>
    public class EmptyFilterExpression : FilterExpression {
        public override string ToString() {
            return "";
        }

        public string Eval(Dictionary<string, ParamType> paramTypes) {
            return "";
        }
    }


    public class FilterJoinExpression : FilterExpression {
        public FilterExpression LHS, RHS;
        public FilterJoinType Op;

        public FilterJoinExpression(FilterExpression LHS, FilterExpression RHS, FilterJoinType Op) {
            this.LHS = LHS;
            this.RHS = RHS;
            this.Op = Op;
        }

        public override string ToString()
        {
            return $"({Op} {LHS} {RHS})";
        }

        public string Eval(Dictionary<string, ParamType> paramTypes) {
            return $"{LHS.Eval(paramTypes)} {FilterEnumResolution.ResolveJoin(Op)} {RHS.Eval(paramTypes)}";
        }
    }

    public class FilterLeafExpression : FilterExpression {
        public string parameter;
        public string value;
        public FilterOpType Op;

        public FilterLeafExpression(string parameter, string value, FilterOpType Op) {
            this.parameter = parameter;
            this.value = value;
            this.Op = Op;
        }

        override public string ToString() {
            return $"({Op} {parameter} {value})";
        }

        public string Eval(Dictionary<string, ParamType> paramTypes) {
            string paramType;
            try { 
                paramType = FilterEnumResolution.ResolveParamType(paramTypes[parameter]); 
            } catch {
                paramType = FilterEnumResolution.ResolveParamType(ParamType.Output); // Assume that a variable is an output if a specification is not provided.
            }
            if (Op == FilterOpType.TopN) {
                return $"id IN (SELECT id FROM solution WHERE project_name=$projectName ORDER BY json_extract({paramType}, '$.{parameter}') DESC LIMIT {value})";
            } else if (Op == FilterOpType.BottomN) {
                return $"id IN (SELECT id FROM solution WHERE project_name=$projectName ORDER BY json_extract({paramType}, '$.{parameter}') ASC LIMIT {value})";
            } else {
                return $"json_extract({paramType}, '$.{parameter}') {FilterEnumResolution.ResolveOp(Op)} {value}";
            }
        }
    }

    // TODO: rewrite the following components to use the expression classes.

    public class Filter : GH_Component
    {
        FilterOpType filterOperator;
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
            "Morpho", "Fitness Functions")
        {
            this.filterOperator = FilterOpType.GreaterThan;
            this.NickName = FilterEnumResolution.ResolveOp(FilterOpType.GreaterThan);
        }

        public override void
        AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            // Each menu item sets a value from the FilterOpType 
            foreach (FilterOpType op in new FilterOpType[]{FilterOpType.GreaterThan, FilterOpType.LesserThan, FilterOpType.GreaterThanOrEqual, FilterOpType.LesserThanOrEqual, FilterOpType.NotEqual, FilterOpType.Equal, FilterOpType.BottomN, FilterOpType.TopN}) {
                Menu_AppendItem(menu, op.ToString(), delegate (Object o, EventArgs e) {
                    this.filterOperator = op;
                    this.NickName = this.filterOperator.ToString();
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
            pManager.AddGenericParameter("filter", "Filter", "Definition of a filter", GH_ParamAccess.item);
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
            DA.SetData(0, new FilterLeafExpression(parameterName, filterValue, filterOperator));
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
          : base("Filter AND", "Filter AND",
            "Joins two or more fitness filters with a binary AND",
            "Morpho", "Fitness Functions")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Fitness Filters", "Fitness Filters", "Set of fitness filters to be joined together.", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("filter", "Filter", "Definition of a filter", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<FilterExpression> fitnessFilters = new List<FilterExpression>();
            DA.GetDataList(0, fitnessFilters);
            FilterExpression f = null;
            foreach (var filter in fitnessFilters) {
                if (f == null) {
                    f = filter;
                } else {
                    var joinExpression = new FilterJoinExpression(f, filter, FilterJoinType.AND);
                    f = joinExpression;
                }
            }
            DA.SetData(0, f);
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
          : base("Filter OR", "Filter OR",
            "Joins two or more fitness filters with a boolean OR",
            "Morpho", "Fitness Functions")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Fitness Filters", "Fitness Filters", "Set of fitness filters to be joined together.", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("filter", "Filter", "Definition of a filter", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<FilterExpression> fitnessFilters = new List<FilterExpression>();
            DA.GetDataList(0, fitnessFilters);
            FilterExpression f = null;
            foreach (var filter in fitnessFilters) {
                if (f == null) {
                    f = filter;
                } else {
                    var joinExpression = new FilterJoinExpression(f, filter, FilterJoinType.OR);
                    f = joinExpression;
                }
            }
            DA.SetData(0, f);
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
