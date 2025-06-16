using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using Grasshopper.Kernel;

namespace morpho
{

    public class SaveToPopulation : GH_Component
    {

        public struct SerializableSolution
        {
            public Dictionary<string, double> input_parameters;
            public Dictionary<string, double> output_parameters;
        }

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public SaveToPopulation()
          : base("Save to Population", "Save to Population",
            "Saves aggregated data to disk.",
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
            pManager.AddGenericParameter("Aggregated Data", "Aggregated Data", "Aggregated data to be saved.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Directory", "Directory", "Directory to save the data under.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Enabled", "Enabled", "Enables the Component", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
        }

        private enum ParameterCheckResult {
            NoProject,
            Valid,
            Invalid,
        };

        private ParameterCheckResult InputParameterCheck(MorphoAggregatedData solution, DirectoryParameters directory) {
            var db = new DBOps(directory);
            db.SetupDB();
            try {
                var inputParams = db.GetInputParameters(directory.projectName);
                foreach (var inputParameterPair in solution.inputs) {
                    if (!inputParams.ContainsKey(inputParameterPair.Key)) {
                        return ParameterCheckResult.Invalid;
                    }
                }
                return ParameterCheckResult.Valid;
            } catch (DBOps.ProjectNotFoundException) {
                return ParameterCheckResult.NoProject;
            }
        }

        protected static void checkError(bool success, string errorMessage)
        {
            if (!success)
                throw new Exception(errorMessage);
        }

        protected static void checkParameterError(bool success) {
            if (!success)
                throw new ParameterException();
        }

        protected static T GetParameter<T>(IGH_DataAccess DA, int position)
        {
            T data_item = default;
            checkParameterError(DA.GetData(position, ref data_item));
            return data_item;
        }

        /// <summary>
        /// Takes a bitmap image (bitmapPair) and saves it to a directory (directoryParameters), under a solutionId.
        /// </summary>
        /// <param name="bitmapPair">The <see cref="NamedBitmap"/> to save.</param>
        /// <param name="directoryParameters">The directory to save the file to.</param>
        /// <param name="solutionId">The solution id to save the file under.</param>
        /// <returns>The local file path to which the file is saved to, within the directory.</returns>
        /// <exception cref="Exception">Thrown when the bitmap is null.</exception>
        private static string SaveImage(NamedBitmap bitmapPair, DirectoryParameters directoryParameters, string solutionId)
        {
            if (bitmapPair.bitmap == null)
            {
                throw new Exception("Viewport not captured.");
            }

            if (!Directory.Exists(Path.Combine(directoryParameters.directory, bitmapPair.name)))
            {
                Directory.CreateDirectory(Path.Combine(directoryParameters.directory, bitmapPair.name));
            }

            var localFilePath = Path.Combine(bitmapPair.name, solutionId);
            localFilePath = Path.ChangeExtension(localFilePath, ".png");

            var filePath = Path.Combine(directoryParameters.directory, localFilePath);
            bitmapPair.bitmap.Save(filePath, ImageFormat.Png);
            return localFilePath;
        }

        /// <summary>
        /// Saves a string to a file in a directory under a specific tag and solution id.
        /// </summary>
        /// <param name="fileTag">Name of the file's tag</param>
        /// <param name="fileContents">The contents to be saved</param>
        /// <param name="directoryParameters">The directory to save the file to</param>
        /// <param name="solutionId">The solution id to save the file under</param>
        /// <returns>The local file path to which the file is saved to, within the directory.</returns>
        private static string SaveFile(string fileTag, string fileContents, DirectoryParameters directoryParameters, string solutionId)
        {
            if (!Directory.Exists(Path.Combine(directoryParameters.directory, fileTag)))
            {
                Directory.CreateDirectory(Path.Combine(directoryParameters.directory, fileTag));
            }
            var localFilePath = Path.Combine(fileTag, solutionId);
            localFilePath = Path.ChangeExtension(localFilePath, ".txt");

            var filePath = Path.Combine(directoryParameters.directory, localFilePath);
            var fd = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
            var stream = new StreamWriter(fd);

            stream.Write(fileContents);
            stream.Flush();
            stream.Close();

            return localFilePath;
        }
        
        protected static void SaveToCSV(string filename, MorphoAggregatedData solution)
        {
            var fd = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Read);
            var fileLength = fd.Length;
            fd.Close();

            var file = new StreamWriter(filename, append: true);

            // Append a header if the file is empty
            if (fileLength == 0)
            {
                var headerStringBuilder = new StringBuilder();
                foreach (var input in solution.inputs)
                {
                    headerStringBuilder.Append($"{input.Key},");
                }
                foreach (var output in solution.outputs)
                {
                    headerStringBuilder.Append($"{output.Key},");
                }
                headerStringBuilder.Remove(headerStringBuilder.Length - 1, 1);
                headerStringBuilder.AppendLine();
                file.Write(headerStringBuilder.ToString());
            }

            // Append the solution to the end of the file
            var dataStringBuilder = new StringBuilder();
            foreach (var input in solution.inputs)
            {
                dataStringBuilder.Append($"{input.Value},");
            }
            foreach (var output in solution.outputs)
            {
                dataStringBuilder.Append($"{output.Value},");
            }
            dataStringBuilder.Remove(dataStringBuilder.Length - 1, 1);
            dataStringBuilder.AppendLine();
            file.Write(dataStringBuilder.ToString());
            file.Close();
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            try {
                MorphoAggregatedData solution = GetParameter<MorphoAggregatedData>(DA, 0);
                var directoryObject = GetParameter<DirectoryParameters>(DA, 1);
                bool enabled = GetParameter<bool>(DA, 2);

                var projectName = System.Security.SecurityElement.Escape(directoryObject.projectName);

                if (!enabled) {
                    return;
                }

                // check if the directory is valid
                if (!Directory.Exists(directoryObject.directory))
                {
                    throw new Exception("Directory does not exist.");
                }

                if (solution.inputs == null || solution.outputs == null)
                {
                    throw new ParameterException();
                }

                // 1. Check if any of the inputs or outputs are empty
                SerializableSolution serializableSolution = new SerializableSolution{ input_parameters = solution.inputs, output_parameters = solution.outputs };
                if (solution.inputs == null || solution.outputs == null)
                {
                    // if anything turns null, return without failing
                    return;
                }

                // 2. Save solution to a CSV first
                SaveToCSV(directoryObject.directory + "/solutions.csv", solution);

                DBOps db = new DBOps(directoryObject);
                
                // 3. Check if the names of input parameters differ for this insert. If it does, error out.
                var inputCheckResult = InputParameterCheck(solution, directoryObject);
                if (inputCheckResult == ParameterCheckResult.Invalid) {
                    throw new Exception("Input parameters do not match initial setup.");
                } else if (inputCheckResult == ParameterCheckResult.NoProject) {
                    db.InsertTableLayout(serializableSolution, solution.files, solution.images.Select(i => i.name).ToArray(), projectName);
                }

                // 4. Check if any of the image-capturing viewports are missing
                foreach (var nb in solution.images)
                {
                    if (nb.bitmap == null)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Viewport not set.");
                        return;
                    }
                }

                // 5. Save the solution to the database, and then use the id we get back to save images and files.
                var (solutionId, scopedId) = db.InsertSolution(serializableSolution, directoryObject.projectName);
                var savedFilePaths = new Dictionary<string, string>();
                foreach (var nb in solution.images)
                {
                    var filepath = SaveImage(nb, directoryObject, scopedId);
                    savedFilePaths.Add(nb.name, filepath);
                    // solution.files.Add(nb.name, filepath); // filepaths are addded to assets later
                }

                foreach (var filePair in solution.files)
                {
                    var filepath = SaveFile(filePair.Key, filePair.Value, directoryObject, scopedId);
                    savedFilePaths.Add(filePair.Key, filepath);
                }

                db.InsertSolutionAssets(solutionId, savedFilePaths);
            } catch (ParameterException) {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Missing Parameters");
            } catch (DBOps.InsertionError e) {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
            } catch (DBOps.ProjectNotFoundException e) {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
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
        protected override Bitmap Icon {
            get {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var stream = assembly.GetManifestResourceStream("ghplugin.icons.save_to_population.png");
                return new Bitmap(stream);
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("3c6102ac-8a0e-49fe-b82a-2b09f0b2acf6");
    }
}
