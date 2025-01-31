using System;
using Grasshopper.Kernel;

namespace ghplugin
{
    using NumberSlider = Grasshopper.Kernel.Special.GH_NumberSlider;

    public class DataTest : GH_Component
    {
        private struct ParameterDefinition {
            public double max, min;
            public string name;
        }

        private ParameterDefinition[] parameterDefinitions;

        private void collectInputSchema() {
            // Grasshopper.Kernel.Special.GH_NumberSlider component = (Grasshopper.Kernel.Special.GH_NumberSlider) this.Component.Params.Input[0].Sources[0];
            this.parameterDefinitions = new ParameterDefinition[this.Params.Input[0].Sources.Count];
            var index = 0;
            foreach (object slider in this.Params.Input[0].Sources) {
                if (slider.GetType() != typeof(Grasshopper.Kernel.Special.GH_NumberSlider) || slider.GetType().ToString() == "GalapagosComponents.GalapagosGeneListObject") {
                    throw new Exception($"Only Number Sliders and Gene Pools are accepted.");
                }

                if (slider.GetType() == typeof(NumberSlider)) {
                    var tempSlider = (NumberSlider ) slider;
                    this.parameterDefinitions[index] = new ParameterDefinition();
                    this.parameterDefinitions[index].max = (double) tempSlider.Slider.Maximum;
                    this.parameterDefinitions[index].min = (double) tempSlider.Slider.Minimum;
                    this.parameterDefinitions[index].name = tempSlider.NickName;
                    index ++;
                } else {
                    dynamic tempSlider = slider;
                    this.parameterDefinitions[index] = new ParameterDefinition();
                    this.parameterDefinitions[index].max = (double) tempSlider.Maximum;
                    this.parameterDefinitions[index].min = (double) tempSlider.Minimum;
                    this.parameterDefinitions[index].name = tempSlider.NickName;
                    index ++;
                }
            }
        }

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
            pManager.AddNumberParameter("Intervals", "Intervals", "Set of Intervals", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Output", "output", "Output of the component", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            this.collectInputSchema();
            string[] output = new string[this.parameterDefinitions.Length];
            for (int i = 0; i < this.parameterDefinitions.Length; i ++) {
                output[i] = $"{this.parameterDefinitions[i].name},{this.parameterDefinitions[i].min},{this.parameterDefinitions[i].max}";
            }
            DA.SetDataList(0,  output);
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