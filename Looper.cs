using System;
using Rhino;

using Grasshopper.Kernel;
using System.Data.SQLite;
using System.Timers;

namespace ghplugin
{

    public delegate void ExpireSolutionDelegate(Boolean recompute);

    public class Looper : GH_Component
    {
        private int iterations;
        private long lastDBIteration;
        private int counter; // unset initially
        private bool flipper;
        private Timer timer; // unset initially

        string directory, projectName; // unset initially
        bool stateSet;

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public Looper()
          : base("Looper", "Looper",
            "Manages iterations",
            "Morpho", "Genetic Search")
        {
            iterations = 0;
            lastDBIteration = -1;
            flipper = false;
            stateSet = false;
        }

        private void setTimerExpired(Object src, ElapsedEventArgs e) {
            var dbIteration = getCurrentDBIteration(directory, projectName);
            if (dbIteration > this.lastDBIteration) {
                Console.WriteLine(counter);
                this.lastDBIteration = dbIteration;
                counter -= 1;
                this.flipper = !this.flipper;
 
                // Cannot call ExpireSolution(true) directly if it's not a UI component. So we do this.
                // Refer to https://discourse.mcneel.com/t/system-invalidoperationexception-cross-thread-operation-not-valid/95176.
                var d = new ExpireSolutionDelegate(ExpireSolution);
                RhinoApp.InvokeOnUiThread(d, true);
            }

            // Reset the timer
            if (counter > 0) {
                timer.Interval = 500; //ms
                timer.Start();
                Console.WriteLine("Timer expired.");
            }
        }

        private long getCurrentDBIteration(string directory, string projectName) {
            try {
                var connBuilder = new SQLiteConnectionStringBuilder();
                connBuilder.DataSource = $"{directory}/solutions.db";
                connBuilder.Version = 3;
                connBuilder.JournalMode = SQLiteJournalModeEnum.Wal;
                connBuilder.LegacyFormat = false;
                connBuilder.Pooling = true;

                using (SQLiteConnection conn = new SQLiteConnection(connBuilder.ToString())) {
                    Console.WriteLine("database was opened by Looper.");
                    conn.Open();
                    string query = $"select COUNT(*) from {projectName}";
                    var command = conn.CreateCommand();
                    command.CommandText = query;
                    Console.WriteLine("database was closed by Looper.");
                    return (long)command.ExecuteScalar();
                }
            } catch(SQLiteException e) {
                // database is locked, return the current iteration.
                if (e.ErrorCode == (int)SQLiteErrorCode.Busy) {
                    return this.lastDBIteration;
                } else {
                    throw e;
                }
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
            pManager.AddBooleanParameter("Start", "Start", "Starts the loop.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Iteration Count", "Iteration Count", "Number of Iterations to Execute", GH_ParamAccess.item);
            pManager.AddTextParameter("Directory", "Directory", "Directory where the data should be saved into.", GH_ParamAccess.item);
            pManager.AddTextParameter("Project Name", "Project Name", "Name of the project.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Fitness", "Fitness", "Sends out a signal by Fitness", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int newIterationCount = 0;
            bool start = false;
            this.directory = "";
            this.projectName = "";

            DA.GetData(0, ref start);
            DA.GetData(1, ref newIterationCount);
            DA.GetData(2, ref directory);
            DA.GetData(3, ref projectName);

            if (!start) {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Looper has not started yet.");
                this.Phase = GH_SolutionPhase.Computed;
                return;
            }

            if (!this.stateSet) {
                if (newIterationCount != this.iterations) {
                    this.iterations = newIterationCount;
                    this.counter = this.iterations - 1;
                }

                if (start) {
                    // initialize the timer here
                    this.timer = new Timer(3000); // 3s
                    this.timer.Elapsed += setTimerExpired;
                    this.timer.AutoReset = false;
                    this.timer.Start();
                }

                this.stateSet = true;
            } else {
                DA.SetData(0, this.flipper);
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("fc2ea5d9-072b-4427-803d-bb9efe37f44a");
    }
}