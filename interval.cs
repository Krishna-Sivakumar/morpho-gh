using System;
using System.Timers;
using Newtonsoft.Json;

using Grasshopper.Kernel;
using Rhino;
using System.Collections.Generic;
using System.Linq;

namespace morpho
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
			: base("Interval", "Interval",
				"Creates an interval from numerical inputs",
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
			pManager.AddNumberParameter("Start", "start", "Start of Interval", GH_ParamAccess.item);
			pManager.AddNumberParameter("End", "end", "End of Interval", GH_ParamAccess.item);
			// TODO setup a step parameter here
			Params.Input[1].Optional = true;
		}

		protected override void RegisterOutputParams(GH_OutputParamManager pManager)
		{
			pManager.AddTextParameter("Interval", "interval", "Interval generated", GH_ParamAccess.item);
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
				interval.name = NickName;

				if (interval.start > interval.end)
				{
					interval.end = interval.start;
				}
				interval.is_constant = interval.start == interval.end;

				DA.SetData(0, JsonConvert.SerializeObject(interval));
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
			pManager.AddIntegerParameter("Count", "Count", "Number of intervals to generate", GH_ParamAccess.item);
			// TODO setup a step parameter here
			Params.Input[1].Optional = true;
		}

		protected override void RegisterOutputParams(GH_OutputParamManager pManager)
		{
			pManager.AddTextParameter("Intervals", "Intervals", "Intervals generated", GH_ParamAccess.list);
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

				// we generate intervals with the name of form "name_index+1"
				var encodedIntervals = new List<string>();
				for (int i = 0; i < intervalCount; i ++) {
					var temp_interval = interval;
					temp_interval.name = $"{interval.name}_{i+1}";
					encodedIntervals.Add(JsonConvert.SerializeObject(temp_interval));
				}

				DA.SetDataList(0, encodedIntervals);
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

}