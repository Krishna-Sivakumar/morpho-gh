using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace morpho
{
  // TODO fill this up with relevant information pointing to us.
  public class ghpluginInfo : GH_AssemblyInfo
  {
    public override string Name => "Morpho Plugin Info";

    //Return a 24x24 pixel bitmap to represent this GHA library.
    public override Bitmap Icon {
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
        var stream = assembly.GetManifestResourceStream("ghplugin.icons.morpho.png");
        return new Bitmap(stream);
      }
    }

    //Return a short string describing the purpose of this GHA library.
    public override string Description => "";

    public override Guid Id => new Guid("88ba79a7-74e2-4780-a123-cbeedbfd21e9");

    //Return a string identifying you or your company.
    public override string AuthorName => "";

    //Return a string representing your preferred contact details.
    public override string AuthorContact => "";

    //Return a string representing the version.  This returns the same version as the assembly.
    public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
  }
}