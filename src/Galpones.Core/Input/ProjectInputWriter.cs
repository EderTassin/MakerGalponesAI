using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Galpones.Core.Input;

public static class ProjectInputWriter
{
    public static string ToYaml(ProjectInput input)
    {
        ProjectInputLoader.Validate(input);
        return new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build().Serialize(input);
    }

    public static void Save(string path, ProjectInput input)
    {
        var yaml = ToYaml(input);
        var target = Path.GetFullPath(path);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, yaml, new UTF8Encoding(false));
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
