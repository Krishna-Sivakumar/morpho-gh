using System;
using Newtonsoft.Json;

using Grasshopper.Kernel;
using System.Collections.Generic;
using Grasshopper.Kernel.Special;
using Grasshopper.GUI.Base;

namespace morpho
{

	public struct MorphoInterval
	{
		public string name;
		public decimal start;
		public decimal end;
		public bool is_constant;
		public double step;

        public override string ToString()
        {
			return JsonConvert.SerializeObject(this);
        }

		public static MorphoInterval FromSlider(GH_NumberSlider slider) {
			return new MorphoInterval{
				name = slider.NickName,
				start = slider.Slider.Minimum,
				end = slider.Slider.Maximum,
				is_constant = slider.Slider.Maximum == slider.Slider.Minimum,
				step = slider.Slider.Type == GH_SliderAccuracy.Integer ? 1 :  Math.Pow(10, -slider.Slider.DecimalPlaces),
			};
		}

		public static List<MorphoInterval> CollectIntervals(IGH_Param param) {
			var results = new List<MorphoInterval>();
			if (param.SourceCount == 0 || param == null) {
				throw new ParameterException($"Missing parameter {param.Name}");
			}
			foreach (var source in param.Sources) {
				if (source.GetType() != typeof(GH_NumberSlider)) {
					throw new ParameterException($"One of the parameters to 'Intervals' is not a number slider or a gene pool.");
				}
				results.Add(
					FromSlider((GH_NumberSlider)source)
				);
			}
			return results;
		}
	}

	/*
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
			: base("Interval", "Interval",
				"Creates an interval from numerical inputs",
				"Morpho", "Genetic Search")
		{
			interval = new MorphoInterval { start = 0, end = 0, is_constant = false };
		}

		/// <summary>
		/// Registers all the input parameters for this component.
		/// </summary>
		protected override void RegisterInputParams(GH_InputParamManager pManager)
		{
			pManager.AddNumberParameter("Start", "start", "Start of Interval", GH_ParamAccess.item);
			pManager.AddNumberParameter("End", "end", "End of Interval", GH_ParamAccess.item);
			pManager.AddNumberParameter("Step", "step", "The smallest unit change in the interval", GH_ParamAccess.item);
			// TODO setup a step parameter here
			Params.Input[1].Optional = true;
		}

		protected override void RegisterOutputParams(GH_OutputParamManager pManager)
		{
			pManager.AddGenericParameter("Interval", "interval", "Interval generated", GH_ParamAccess.item);
		}

		protected static void checkError(bool success)
		{
			if (!success)
				throw new ParameterException();
		}

		protected static T GetParameter<T>(IGH_DataAccess DA, string fieldName)
		{
			T data_item = default;
			checkError(DA.GetData(fieldName, ref data_item));
			return data_item;
		}

		private Timer debounce;

		/// <summary>
		/// An event trigger to recompute the interval component when the nickname changes.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		protected void expireSolution(object sender, object args)
		{
			// we do not want to call ExpireSolution too many times on a nickname change. So we debounce the input by a second.
			if (debounce == null)
			{
				debounce = new Timer(1000);
				debounce.AutoReset = false;
				debounce.Elapsed += (object s, ElapsedEventArgs e) =>
				{
					// Cannot call ExpireSolution(true) directly if it's not a UI component. So we do this.
					// Refer to https://discourse.mcneel.com/t/system-invalidoperationexception-cross-thread-operation-not-valid/95176.
					var d = new ExpireSolutionDelegate(ExpireSolution);
					RhinoApp.InvokeOnUiThread(d, true);
					debounce.Stop();
					debounce = null;
				};
			}
			else
			{
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
			// this is needed to respond to nickname changes
			this.ObjectChanged += expireSolution;

			try
			{
				interval.start = GetParameter<double>(DA, "Start");
				interval.end = GetParameter<double>(DA, "End");
				interval.step = GetParameter<double>(DA, "Step");
				interval.name = NickName;

				// if the start and the end is the same, the interval is a constant.
				if (interval.start > interval.end)
				{
					interval.end = interval.start;
					interval.step = 0;
				}
				interval.is_constant = interval.start == interval.end;

				// the step can only be as large as the gap between the start and the end.
				interval.step = Math.Min(interval.step, Math.Abs(interval.end - interval.start));

				DA.SetData(0, interval);
			}
			catch (ParameterException)
			{
				AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Parameters Missing.");
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
		public override Guid ComponentGuid => new Guid("2d55cacf-9a72-46d9-b0cc-a46ad1081145");
	}

	// the only thing this component does differently is generate multiple intervals with the same name and range but different indices
	public class MultiInterval : GH_Component
	{

		public MorphoInterval interval;

		/// <summary>
		/// Each implementation of GH_Component must provide a public 
		/// constructor without any arguments.
		/// Category represents the Tab in which the component will appear, 
		/// Subcategory the panel. If you use non-existing tab or panel names, 
		/// new tabs/panels will automatically be created.
		/// </summary>
		public MultiInterval()
			: base("MultiInterval", "MultiInterval",
				"Creates multiple identical intervals from numerical limits",
				"Morpho", "Genetic Search")
		{
			interval = new MorphoInterval { start = 0, end = 0, is_constant = false };
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
			pManager.AddNumberParameter("Start", "Start", "Start of Interval", GH_ParamAccess.item);
			pManager.AddNumberParameter("End", "End", "End of Interval", GH_ParamAccess.item);
			pManager.AddNumberParameter("Step", "step", "Size of step", GH_ParamAccess.item);
			pManager.AddIntegerParameter("Count", "Count", "Number of intervals to generate", GH_ParamAccess.item);
			Params.Input[1].Optional = true;
		}

		protected override void RegisterOutputParams(GH_OutputParamManager pManager)
		{
			pManager.AddGenericParameter("Intervals", "Intervals", "Intervals generated", GH_ParamAccess.list);
		}

		private Timer debounce;

		protected static void checkError(bool success)
		{
			if (!success)
				throw new ParameterException();
		}

		protected static T GetParameter<T>(IGH_DataAccess DA, string fieldName)
		{
			T data_item = default;
			checkError(DA.GetData(fieldName, ref data_item));
			return data_item;
		}

		protected void expireSolution(object sender, object args)
		{
			// we do not want to call ExpireSolution too many times on a nickname change. So we debounce the input by a second.
			if (debounce == null)
			{
				debounce = new Timer(1000);
				debounce.AutoReset = false;
				debounce.Elapsed += (object s, ElapsedEventArgs e) =>
				{
					// Cannot call ExpireSolution(true) directly if it's not a UI component. So we do this.
					// Refer to https://discourse.mcneel.com/t/system-invalidoperationexception-cross-thread-operation-not-valid/95176.
					var d = new ExpireSolutionDelegate(ExpireSolution);
					RhinoApp.InvokeOnUiThread(d, true);
					debounce.Stop();
					debounce = null;
				};
			}
			else
			{
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
			// this is needed to respond to nickname changes
			this.ObjectChanged += expireSolution;

			try
			{
				interval.start = GetParameter<double>(DA, "Start");
				interval.end = GetParameter<double>(DA, "End");
				var intervalCount = GetParameter<int>(DA, "Count");
				interval.name = NickName;

				if (interval.start > interval.end)
				{
					interval.end = interval.start;
				}
				interval.is_constant = interval.start == interval.end;

				// we generate multiple intervals with the name of form "name_index+1"
				var intervals = new List<MorphoInterval>();
				for (int i = 0; i < intervalCount; i ++) {
					var temp_interval = interval;
					temp_interval.name = $"{interval.name}_{i+1}";
					intervals.Append(temp_interval);
				}

				DA.SetDataList(0, intervals);
			}
			catch (ParameterException)
			{
				AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Parameters Missing.");
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
		public override Guid ComponentGuid => new Guid("efd400f2-5e07-40c6-889e-7302f0841afa");
	}
	*/
}