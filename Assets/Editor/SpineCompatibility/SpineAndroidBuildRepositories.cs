#if UNITY_ANDROID
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.Android;

// Project-local build infrastructure, deliberately outside the redistributable SDK.
public sealed class SpineAndroidBuildRepositories : IPostGenerateGradleAndroidProject {
    public int callbackOrder { get { return 1000; } }

    public void OnPostGenerateGradleAndroidProject(string path) {
        var root = Directory.GetParent(path).FullName;
        var settingsPath = Path.Combine(root, "settings.gradle");
        const string marker = "// Spine compatibility validation: domestic Maven mirrors";
        var settings = File.ReadAllText(settingsPath);
        if (!settings.Contains(marker)) {
            var mirrors = "\n        " + marker + "\n"
                + "        maven { url 'https://maven.aliyun.com/repository/google' }\n"
                + "        maven { url 'https://maven.aliyun.com/repository/public' }\n"
                + "        maven { url 'https://maven.aliyun.com/repository/gradle-plugin' }\n";
            settings = Regex.Replace(settings, @"\brepositories\s*\{", match => match.Value + mirrors);
            File.WriteAllText(settingsPath, settings, new UTF8Encoding(false));
        }
        var propertiesPath = Path.Combine(root, "gradle.properties");
        var properties = File.ReadAllText(propertiesPath);
        if (!properties.Contains("org.gradle.internal.http.socketTimeout")) {
            properties += "\nsystemProp.org.gradle.internal.http.connectionTimeout=15000\n"
                + "systemProp.org.gradle.internal.http.socketTimeout=30000\n";
            File.WriteAllText(propertiesPath, properties, new UTF8Encoding(false));
        }
    }
}
#endif
