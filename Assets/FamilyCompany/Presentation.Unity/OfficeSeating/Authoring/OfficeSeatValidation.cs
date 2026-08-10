using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace FamilyCompany.Presentation.Unity.OfficeSeating.Authoring
{
    public enum OfficeSeatValidationSeverity
    {
        Warning = 0,
        Error = 1
    }

    public sealed class OfficeSeatValidationIssue
    {
        public OfficeSeatValidationIssue(
            OfficeSeatValidationSeverity severity,
            string code,
            string seatId,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            SeatId = seatId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public OfficeSeatValidationSeverity Severity { get; }
        public string Code { get; }
        public string SeatId { get; }
        public string Message { get; }
    }

    public sealed class OfficeSeatValidationReport
    {
        private readonly List<OfficeSeatValidationIssue> _issues =
            new List<OfficeSeatValidationIssue>();

        public IReadOnlyList<OfficeSeatValidationIssue> Issues =>
            new ReadOnlyCollection<OfficeSeatValidationIssue>(_issues);

        public bool HasErrors
        {
            get
            {
                foreach (var issue in _issues)
                {
                    if (issue.Severity == OfficeSeatValidationSeverity.Error) return true;
                }

                return false;
            }
        }

        public void AddError(string code, string seatId, string message)
        {
            _issues.Add(new OfficeSeatValidationIssue(
                OfficeSeatValidationSeverity.Error,
                code,
                seatId,
                message));
        }

        public void AddWarning(string code, string seatId, string message)
        {
            _issues.Add(new OfficeSeatValidationIssue(
                OfficeSeatValidationSeverity.Warning,
                code,
                seatId,
                message));
        }

        public void Merge(OfficeSeatValidationReport other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            foreach (var issue in other._issues) _issues.Add(issue);
        }

        public string FormatErrors()
        {
            var builder = new StringBuilder();
            foreach (var issue in _issues)
            {
                if (issue.Severity != OfficeSeatValidationSeverity.Error) continue;
                if (builder.Length > 0) builder.AppendLine();
                builder.Append('[').Append(issue.Code).Append("] ").Append(issue.Message);
            }

            return builder.ToString();
        }
    }
}
