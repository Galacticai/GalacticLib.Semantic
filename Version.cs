using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace GalacticLib.Semantic {
    /// <summary> Semantic version matching the guidelines in semver.org </summary>
    public class Version {
        /// <summary> Collection of version regex-related things </summary>
        public record VersionRegex {
            /// <summary> Check whether a string is a version that follows the semtantic version guidelines </summary>
            /// <param name="versionString"> Target <see cref="string"/> </param>
            /// <returns> true if the string matches the <see cref="Complete"/> semtantic version regex </returns>
            public static bool IsSemantic(string versionString)
                => Regex.IsMatch(versionString.Trim(), Complete);

            public const string Number
                = @"[0-9]";
            public const string AlphanumericDash
                = @"[0-9A-Za-z-]";
            public const string AlphanumericDashDot
                = @"[0-9A-Za-z-.]";
            /// <summary> (X.Y.Z)-BuildType+Build </summary> 
            public const string XYZ
                = $@"({Number}+)\.({Number}+)\.({Number}+)";
            /// <summary> X.Y.Z-(BuildType)+Build </summary> 
            public const string BuildType
                = $@"(?:-({AlphanumericDash}+(?:\.{AlphanumericDashDot}+)*))";
            /// <summary> X.Y.Z-BuildType+(Build) </summary> 
            public const string Build
                = $@"?(?:\+{AlphanumericDashDot}+)?";
            /// <summary> (X.Y.Z-BuildType+Build) </summary> 
            public const string Complete = XYZ + BuildType + Build;
        }
        /// <summary> The name of the version part
        /// <br/> Major.Minor.Patch-BuildType+Build </summary>
        public enum VersionPartType {
            Major, Minor, Patch, BuildType, Build
        }
        /// <summary> Collection of preset BuildTypes </summary>
        public record BuildTypes {
            public static readonly string
                Alpha = "alpha", Beta = "beta",
                Dev = "dev", Development = "development",
                Pre = "pre", PreRelease = "pre-release",
                Release = "release", ReleaseCandidate = "rc",
                Stable = "stable", Unstable = "unstable",
                Test = "test", Testing = "testing";
        }

        int _Major;
        /// <summary> (Major).Minor.Patch-BuildType+Build </summary>
        public int Major { get => _Major; set => _Major = value < 0 ? 0 : value; }
        int _Minor;
        /// <summary> Major.(Minor).Patch-BuildType+Build </summary>
        public int Minor { get => _Minor; set => _Minor = value < 0 ? 0 : value; }
        int _Patch;
        /// <summary> Major.Minor.(Patch)-BuildType+Build </summary>
        public int Patch { get => _Patch; set => _Patch = value < 0 ? 0 : value; }
        /// <summary> Major.Minor.Patch-(BuildType)+Build </summary>
        public string? BuildType { get; set; }
        /// <summary> Major.Minor.Patch-BuildType+(Build) </summary>
        public string? Build { get; set; }


        /// <summary> Semantic version matching the guidelines in semver.org </summary>
        public Version(
                int major, int minor, int patch,
                string? buildType, string? build) {
            Major = major;
            Minor = minor;
            Patch = patch;
            BuildType = buildType;
            Build = build;
        }
        /// <summary> Semantic version matching the guidelines in semver.org </summary>
        public Version(int major, int minor, int patch, string? buildType)
                : this(major, minor, patch, buildType, null) { }
        /// <summary> Semantic version matching the guidelines in semver.org </summary>
        public Version(int major, int minor, int patch)
                : this(major, minor, patch, null, null) { }
        /// <summary> Semantic version matching the guidelines in semver.org </summary>
        public Version(int major, int minor)
                : this(major, minor, 0, null, null) { }
        /// <summary> Semantic version matching the guidelines in semver.org </summary>
        public Version(int major)
                : this(major, 0, 0, null, null) { }
        /// <summary> Semantic version matching the guidelines in semver.org </summary>
        public Version()
                : this(0, 0, 0, null, null) { }
        /// <summary> Semantic version matching the guidelines in semver.org 
        /// <br/> From a <see cref="string"/> </summary>
        public Version(string versionString)
                : this(versionString, false) { }
        /// <summary> Semantic version matching the guidelines in semver.org 
        /// <br/> From a <see cref="string"/> </summary>
        public Version(string versionString, bool forceSemantic) {
            if (forceSemantic && !VersionRegex.IsSemantic(versionString))
                throw new ArgumentException("The provided version string does not follow the semantic version guidelines");

            versionString = versionString.Trim();
            string?[] parts = new string?[5] { null, null, null, null, null };
            int partIndex = 0;

            StringBuilder partBuilder = new();

            bool IsXYZ() => (partIndex <= (int)VersionPartType.Patch);

            void ApplyPart() {
                string part = (parts[partIndex] = partBuilder.ToString());
                if (!string.IsNullOrEmpty(part)) {
                    VersionPartType partType = (VersionPartType)partIndex;

                    PropertyInfo? property
                        = typeof(Version)
                        .GetProperty(partType.ToString());

                    //? Shall never happen but it is possible if a typo is introduced
                    if (property == null)
                        throw new Exception("(BUG!) Version part type name is invalid."
                            + Environment.NewLine + "If you see this, please contact the developer.");

                    //? Make int for XYZ
                    if (IsXYZ()) {
                        int.TryParse(part, out int partValue);
                        property.SetValue(this, partValue);
                    }
                    //? keep as {string?} for BuildType, Build
                    else property.SetValue(this, part);
                }
                partIndex++;
                partBuilder = new();
            }

            for (int i = 0; i < versionString.Length && partIndex < parts.Length; i++) {
                char c = versionString[i];
                if (c == ' ') { ApplyPart(); break; }

                if (IsXYZ()) {
                    if (char.IsDigit(c)) { partBuilder.Append(c); continue; }
                    if (c == '.') { ApplyPart(); continue; }
                    ApplyPart();
                    partIndex = (int)VersionPartType.BuildType;
                    if (c != '-' && c != '+') partBuilder.Append(c);
                } else {
                    if (c == '+' && partIndex < (int)VersionPartType.Build) {
                        ApplyPart(); continue;
                    }
                    if (!Regex.IsMatch(c.ToString(), VersionRegex.AlphanumericDashDot)) {
                        ApplyPart(); break;
                    }
                    partBuilder.Append(c);
                }
                if (i == versionString.Length - 1) ApplyPart();
            }
        }

        public Version Parse(string versionString)
            => new(versionString);

        /// <summary> Convert to <see cref="System.Version"/> 
        /// <br/> ⚠️ Warning: <see cref="System.Version"/> does not support the <see cref="BuildType"/> and <see cref="Build"/> so they are lost in the conversion process
        /// <br/> Note: <see cref="Version"/> does not support <see cref="System.Version.Revision"/> so it is replaced by 0 </summary>
        /// <returns> Major.Minor.Patch.0 (as <see cref="System.Version"/>) </returns>
        public virtual System.Version ToVersion()
            => new(Major, Minor, Patch, 0);

        public override int GetHashCode() {
            HashCode code = new();
            code.Add(Major);
            code.Add(Minor);
            code.Add(Patch);
            code.Add(BuildType);
            code.Add(Build);
            return code.ToHashCode();
        }
        public override bool Equals(object? obj) {
            if (obj is not Version other) return false;
            return Major == other.Major
                && Minor == other.Minor
                && Patch == other.Patch
                && BuildType == other.BuildType
                && Build == other.Build;
        }

        /// <summary> Convert to <see cref="string"/> </summary>
        /// <returns> "Major.Minor.Patch-BuildType+Build" 
        /// <br/> Note: BuildType and Build might not always be present </returns>
        public override string ToString() {
            StringBuilder versionSB = new();
            versionSB.Append(Major);
            versionSB.Append('.');
            versionSB.Append(Minor);
            versionSB.Append('.');
            versionSB.Append(Patch);
            if (!string.IsNullOrEmpty(BuildType)) {
                versionSB.Append('-');
                versionSB.Append(BuildType);
            }
            if (!string.IsNullOrEmpty(Build)) {
                versionSB.Append('+');
                versionSB.Append(Build);
            }
            return versionSB.ToString();
        }
    }
}
