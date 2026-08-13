using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace FamilyCompany.Infrastructure.Unity
{
    /// <summary>
    /// 빌드를 다시 만들지 않고 콘텐츠 JSON을 고쳐 보기 위한 외부 데이터 경로다.
    ///
    /// 에디터에서는 프로젝트의 Content 폴더를 그대로 읽는다.
    /// 플레이어에서는 아래 순서로 첫 번째로 존재하는 폴더를 쓴다.
    ///
    /// 1. 환경 변수 <c>FAMILYCOMPANY_LIVE_CONTENT</c>가 가리키는 폴더
    /// 2. exe 폴더 안의 <c>LiveData</c>
    /// 3. exe 폴더의 부모에 있는 <c>FamilyCompany_LiveData</c>
    ///
    /// 3번이 기본값이다. Build-FamilyCompanyWindows.ps1은 승격 단계에서 최종 출력 폴더를
    /// 통째로 교체하므로, 링크를 출력 폴더 안에 두면 빌드마다 사라진다. 부모에 두면 살아남는다.
    ///
    /// 세 후보 모두 <c>History</c> 하위 폴더를 가져야 인정한다. 셋 다 없으면
    /// <see cref="IsEnabled"/>가 false이고 이후로는 디스크를 읽지 않는다.
    ///
    /// <c>DEVELOPMENT_BUILD</c>로 감싸지 않는 이유는 플레이테스트 빌드가 의도적으로
    /// 비-Development이기 때문이다(WindowsPlayerBuild.cs). 대신 위의 명시적 폴더 존재를
    /// opt-in 신호로 쓴다. 배포용 패키지에 그 폴더를 넣지 않으면 기능은 꺼진 상태로 남는다.
    /// </summary>
    public static class LiveContentPath
    {
        public const string EnvironmentVariableName = "FAMILYCOMPANY_LIVE_CONTENT";
        public const string InsideBuildFolderName = "LiveData";
        public const string SiblingFolderName = "FamilyCompany_LiveData";

        /// <summary>후보 폴더가 진짜 콘텐츠 폴더인지 확인하는 표식이다.</summary>
        public const string RequiredSubfolderName = "History";

        private static string _cachedRoot;
        private static bool _rootResolved;

        /// <summary>외부 콘텐츠를 실제로 쓸 수 있으면 true다.</summary>
        public static bool IsEnabled => !string.IsNullOrEmpty(Root);

        /// <summary>해석에 실패하면 빈 문자열이다. 호출부는 항상 내장본으로 되돌아갈 수 있어야 한다.</summary>
        public static string Root
        {
            get
            {
                if (!_rootResolved)
                {
                    _cachedRoot = ResolveRoot();
                    _rootResolved = true;
                }

                return _cachedRoot;
            }
        }

        public static bool RootExists => IsEnabled && Directory.Exists(Root);

        public static bool Exists(string relativePath) =>
            TryGetFullPath(relativePath, out var fullPath) && File.Exists(fullPath);

        public static bool TryGetFullPath(string relativePath, out string fullPath)
        {
            fullPath = null;
            if (!RootExists || string.IsNullOrWhiteSpace(relativePath)) return false;
            fullPath = Path.Combine(Root, relativePath.Replace('\\', '/'));
            return true;
        }

        /// <summary>
        /// 읽기에 실패해도 예외를 던지지 않고 false를 돌려준다.
        /// 빌드나 편집기가 같은 파일을 쓰는 중일 수 있으므로 호출부는 내장본으로 되돌아간다.
        /// </summary>
        public static bool TryReadAllText(string relativePath, out string text)
        {
            text = null;
            if (!TryGetFullPath(relativePath, out var fullPath)) return false;
            if (!File.Exists(fullPath)) return false;

            try
            {
                text = File.ReadAllText(fullPath, Encoding.UTF8);
                return !string.IsNullOrWhiteSpace(text);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>링크를 새로 걸어 경로 자체가 달라졌을 때만 호출한다.</summary>
        public static void InvalidateRootCache()
        {
            _rootResolved = false;
            _cachedRoot = null;
        }

        /// <summary>
        /// Enter Play Mode Options로 도메인 리로드를 끈 상태에서도 static 상태가 남지 않게 한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _rootResolved = false;
            _cachedRoot = null;
        }

        private static string ResolveRoot()
        {
#if UNITY_EDITOR
            // Application.dataPath 는 <프로젝트>/Assets 다.
            var editorRoot = Path.Combine(Application.dataPath, "FamilyCompany", "Content");
            return IsContentFolder(editorRoot) ? editorRoot : string.Empty;
#else
            var configured = SafeGetEnvironmentVariable(EnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(configured) && IsContentFolder(configured))
            {
                return Path.GetFullPath(configured);
            }

            // Application.dataPath 는 <빌드>/<제품명>_Data 다. 그 부모가 exe 폴더다.
            var buildRoot = SafeGetParent(Application.dataPath);
            if (buildRoot == null) return string.Empty;

            var insideBuild = Path.Combine(buildRoot.FullName, InsideBuildFolderName);
            if (IsContentFolder(insideBuild)) return insideBuild;

            if (buildRoot.Parent != null)
            {
                var sibling = Path.Combine(buildRoot.Parent.FullName, SiblingFolderName);
                if (IsContentFolder(sibling)) return sibling;
            }

            return string.Empty;
#endif
        }

        /// <summary>우연히 같은 이름의 폴더가 있어도 켜지지 않도록 내용을 확인한다.</summary>
        private static bool IsContentFolder(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return false;

            try
            {
                return Directory.Exists(candidate) &&
                       Directory.Exists(Path.Combine(candidate, RequiredSubfolderName));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string SafeGetEnvironmentVariable(string name)
        {
            try
            {
                return Environment.GetEnvironmentVariable(name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static DirectoryInfo SafeGetParent(string path)
        {
            try
            {
                return Directory.GetParent(path);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
