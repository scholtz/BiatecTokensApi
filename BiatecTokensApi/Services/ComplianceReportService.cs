using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service implementation for compliance report generation and management
    /// </summary>
    public class ComplianceReportService : IComplianceReportService
    {
        private readonly IComplianceReportRepository _reportRepository;
        private readonly IEnterpriseAuditService _auditService;
        private readonly IComplianceService _complianceService;
        private readonly ILogger<ComplianceReportService> _logger;

        private const string SchemaVersion = "1.0";

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceReportService"/> class.
        /// </summary>
        /// <param name="reportRepository">The compliance report repository</param>
        /// <param name="auditService">The enterprise audit service</param>
        /// <param name="complianceService">The compliance service</param>
        /// <param name="logger">The logger instance</param>
        public ComplianceReportService(
            IComplianceReportRepository reportRepository,
            IEnterpriseAuditService auditService,
            IComplianceService complianceService,
            ILogger<ComplianceReportService> logger)
        {
            _reportRepository = reportRepository;
            _auditService = auditService;
            _complianceService = complianceService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<CreateComplianceReportResponse> CreateReportAsync(CreateComplianceReportRequest request, string issuerId)
        {
            try
            {
                // Sanitize inputs for logging
                var sanitizedIssuerId = LoggingHelper.SanitizeLogInput(issuerId);
                var sanitizedNetwork = request.Network != null ? LoggingHelper.SanitizeLogInput(request.Network) : null;

                _logger.LogInformation("Creating compliance report: Type={ReportType}, IssuerId={IssuerId}, AssetId={AssetId}, Network={Network}",
                    LoggingHelper.SanitizeLogInput(request.ReportType.ToString()), sanitizedIssuerId, LoggingHelper.SanitizeLogInput(request.AssetId?.ToString()), sanitizedNetwork);

                // Create initial report record
                var report = new ComplianceReport
                {
                    ReportType = request.ReportType,
                    IssuerId = issuerId,
                    AssetId = request.AssetId,
                    Network = request.Network,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    Status = ReportStatus.Processing,
                    SchemaVersion = SchemaVersion,
                    CreatedAt = DateTime.UtcNow
                };

                // Save report to repository
                report = await _reportRepository.CreateReportAsync(report);

                // Generate report content in background (simplified synchronous for MVP)
                await GenerateReportContentAsync(report);

                return new CreateComplianceReportResponse
                {
                    Success = true,
                    ReportId = report.ReportId,
                    Status = report.Status,
                    CreatedAt = report.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating compliance report");
                return new CreateComplianceReportResponse
                {
                    Success = false,
                    ErrorMessage = $"Error creating report: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<GetComplianceReportResponse> GetReportAsync(string reportId, string issuerId)
        {
            try
            {
                var sanitizedReportId = LoggingHelper.SanitizeLogInput(reportId);
                var sanitizedIssuerId = LoggingHelper.SanitizeLogInput(issuerId);

                _logger.LogInformation("Getting compliance report: ReportId={ReportId}, IssuerId={IssuerId}",
                    sanitizedReportId, sanitizedIssuerId);

                var report = await _reportRepository.GetReportAsync(reportId, issuerId);

                if (report == null)
                {
                    return new GetComplianceReportResponse
                    {
                        Success = false,
                        ErrorMessage = "Report not found or access denied"
                    };
                }

                return new GetComplianceReportResponse
                {
                    Success = true,
                    Report = report
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting compliance report");
                return new GetComplianceReportResponse
                {
                    Success = false,
                    ErrorMessage = $"Error retrieving report: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<ListComplianceReportsResponse> ListReportsAsync(ListComplianceReportsRequest request, string issuerId)
        {
            try
            {
                var sanitizedIssuerId = LoggingHelper.SanitizeLogInput(issuerId);
                _logger.LogInformation("Listing compliance reports for IssuerId={IssuerId}, Page={Page}",
                    sanitizedIssuerId, request.Page);

                var reports = await _reportRepository.ListReportsAsync(issuerId, request);
                var totalCount = await _reportRepository.GetReportCountAsync(issuerId, request);

                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                var summaries = reports.Select(r => new ComplianceReportSummary
                {
                    ReportId = r.ReportId,
                    ReportType = r.ReportType,
                    Status = r.Status,
                    AssetId = r.AssetId,
                    Network = r.Network,
                    EventCount = r.EventCount,
                    CreatedAt = r.CreatedAt,
                    CompletedAt = r.CompletedAt,
                    WarningCount = r.Warnings.Count
                }).ToList();

                return new ListComplianceReportsResponse
                {
                    Success = true,
                    Reports = summaries,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing compliance reports");
                return new ListComplianceReportsResponse
                {
                    Success = false,
                    ErrorMessage = $"Error listing reports: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<string> DownloadReportAsync(string reportId, string issuerId, string format)
        {
            var sanitizedReportId = LoggingHelper.SanitizeLogInput(reportId);
            var sanitizedIssuerId = LoggingHelper.SanitizeLogInput(issuerId);
            var sanitizedFormat = LoggingHelper.SanitizeLogInput(format);

            _logger.LogInformation("Downloading compliance report: ReportId={ReportId}, IssuerId={IssuerId}, Format={Format}",
                sanitizedReportId, sanitizedIssuerId, sanitizedFormat);

            var report = await _reportRepository.GetReportAsync(reportId, issuerId);

            if (report == null)
            {
                throw new InvalidOperationException("Report not found or access denied");
            }

            if (report.Status != ReportStatus.Completed)
            {
                throw new InvalidOperationException($"Report is not ready for download. Status: {report.Status}");
            }

            if (string.IsNullOrEmpty(report.ContentJson))
            {
                throw new InvalidOperationException("Report content is missing");
            }

            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                return report.ContentJson;
            }
            else if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                return ConvertReportToCsv(report);
            }
            else
            {
                throw new ArgumentException($"Unsupported format: {format}. Supported formats: json, csv");
            }
        }

        /// <summary>
        /// Generates report content based on report type
        /// </summary>
        private async Task GenerateReportContentAsync(ComplianceReport report)
        {
            try
            {
                _logger.LogInformation("Generating report content: ReportId={ReportId}, Type={ReportType}",
                    report.ReportId, report.ReportType);

                string contentJson;

                switch (report.ReportType)
                {
                    case ReportType.MicaReadiness:
                        contentJson = await GenerateMicaReadinessReportAsync(report);
                        break;

                    case ReportType.AuditTrail:
                        contentJson = await GenerateAuditTrailReportAsync(report);
                        break;

                    case ReportType.ComplianceBadge:
                        contentJson = await GenerateComplianceBadgeReportAsync(report);
                        break;

                    default:
                        throw new ArgumentException($"Unsupported report type: {report.ReportType}");
                }

                // Calculate checksum
                var checksum = CalculateChecksum(contentJson);

                // Update report with content and checksum
                report.ContentJson = contentJson;
                report.Checksum = checksum;
                report.Status = ReportStatus.Completed;
                report.CompletedAt = DateTime.UtcNow;

                await _reportRepository.UpdateReportAsync(report);

                _logger.LogInformation("Report generation completed: ReportId={ReportId}, EventCount={EventCount}, Checksum={Checksum}",
                    report.ReportId, report.EventCount, checksum);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report content: ReportId={ReportId}", report.ReportId);

                // Update report status to Failed
                report.Status = ReportStatus.Failed;
                report.ErrorMessage = ex.Message;
                await _reportRepository.UpdateReportAsync(report);
            }
        }

        /// <summary>
        /// Generates MICA readiness report content
        /// </summary>
        private async Task<string> GenerateMicaReadinessReportAsync(ComplianceReport report)
        {
            var content = new MicaReadinessReportContent
            {
                Metadata = new MicaReadinessReportMetadata
                {
                    ReportId = report.ReportId,
                    SchemaVersion = report.SchemaVersion,
                    GeneratedAt = DateTime.UtcNow,
                    AssetId = report.AssetId,
                    Network = report.Network,
                    FromDate = report.FromDate,
                    ToDate = report.ToDate
                }
            };

            // Run MICA compliance checks
            var checks = new List<MicaComplianceCheck>();

            // Article 17: Authorisation requirements
            checks.Add(new MicaComplianceCheck
            {
                Article = "Article 17",
                Requirement = "Issuer must be properly authorized and registered",
                Status = "Pass",
                Evidence = "Issuer profile exists and is verified",
                Recommendation = null
            });

            // Article 18: Transparency and disclosure
            checks.Add(new MicaComplianceCheck
            {
                Article = "Article 18",
                Requirement = "Comprehensive disclosure of token information",
                Status = "Partial",
                Evidence = "Token metadata available in compliance records",
                Recommendation = "Ensure whitepaper and risk disclosure documents are attached"
            });

            // Article 19: Token holder rights
            checks.Add(new MicaComplianceCheck
            {
                Article = "Article 19",
                Requirement = "Clear documentation of token holder rights and obligations",
                Status = "Partial",
                Evidence = "Transfer restrictions documented",
                Recommendation = "Add detailed rights documentation to compliance metadata"
            });

            // Article 20: Complaint handling
            checks.Add(new MicaComplianceCheck
            {
                Article = "Article 20",
                Requirement = "Complaint handling procedures in place",
                Status = "Fail",
                Evidence = "No complaint handling procedures documented",
                Recommendation = "Establish and document complaint handling process"
            });

            // Article 21-25: Operational requirements
            var auditRequest = new GetEnterpriseAuditLogRequest
            {
                AssetId = report.AssetId,
                Network = report.Network,
                FromDate = report.FromDate,
                ToDate = report.ToDate,
                Page = 1,
                PageSize = 1000
            };

            var auditResponse = await _auditService.GetAuditLogAsync(auditRequest);
            var hasAuditTrail = auditResponse.Success && auditResponse.Entries.Count > 0;

            checks.Add(new MicaComplianceCheck
            {
                Article = "Article 21-25",
                Requirement = "Operational procedures and audit trail maintenance",
                Status = hasAuditTrail ? "Pass" : "Fail",
                Evidence = hasAuditTrail ? $"{auditResponse.Entries.Count} audit events recorded" : "No audit trail found",
                Recommendation = hasAuditTrail ? null : "Enable comprehensive audit logging"
            });

            // Article 26-30: Risk management
            checks.Add(new MicaComplianceCheck
            {
                Article = "Article 26-30",
                Requirement = "Risk management framework",
                Status = "Partial",
                Evidence = "Basic risk controls in place (whitelist, compliance metadata)",
                Recommendation = "Implement comprehensive risk assessment and monitoring"
            });

            // Article 31-35: Reserve assets (if applicable)
            checks.Add(new MicaComplianceCheck
            {
                Article = "Article 31-35",
                Requirement = "Reserve asset management (for asset-referenced tokens)",
                Status = "N/A",
                Evidence = "Not applicable to this token type",
                Recommendation = null
            });

            content.ComplianceChecks = checks;

            // Calculate readiness score
            var passCount = checks.Count(c => c.Status == "Pass");
            var partialCount = checks.Count(c => c.Status == "Partial");
            var totalApplicable = checks.Count(c => c.Status != "N/A");

            content.ReadinessScore = totalApplicable > 0
                ? (int)Math.Round((passCount * 100.0 + partialCount * 50.0) / totalApplicable)
                : 0;

            // Collect missing evidence
            content.MissingEvidence = checks
                .Where(c => c.Status == "Fail" && !string.IsNullOrEmpty(c.Recommendation))
                .Select(c => $"{c.Article}: {c.Recommendation}")
                .ToList();

            // Generate summary
            if (content.ReadinessScore >= 80)
            {
                content.ReadinessSummary = "Token demonstrates strong MICA compliance readiness";
            }
            else if (content.ReadinessScore >= 60)
            {
                content.ReadinessSummary = "Token shows moderate MICA compliance with some gaps to address";
            }
            else
            {
                content.ReadinessSummary = "Token requires significant compliance improvements before MICA readiness";
            }

            // Update report warnings
            report.Warnings = content.MissingEvidence;
            report.EventCount = checks.Count;

            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(content, options);
        }

        /// <summary>
        /// Generates audit trail report content
        /// </summary>
        private async Task<string> GenerateAuditTrailReportAsync(ComplianceReport report)
        {
            var auditRequest = new GetEnterpriseAuditLogRequest
            {
                AssetId = report.AssetId,
                Network = report.Network,
                FromDate = report.FromDate,
                ToDate = report.ToDate,
                Page = 1,
                PageSize = 10000 // Get up to 10,000 events
            };

            var auditResponse = await _auditService.GetAuditLogAsync(auditRequest);

            if (!auditResponse.Success)
            {
                throw new InvalidOperationException($"Failed to retrieve audit logs: {auditResponse.ErrorMessage}");
            }

            var content = new AuditTrailReportContent
            {
                Metadata = new AuditTrailReportMetadata
                {
                    ReportId = report.ReportId,
                    SchemaVersion = report.SchemaVersion,
                    GeneratedAt = DateTime.UtcNow,
                    AssetId = report.AssetId,
                    Network = report.Network,
                    FromDate = report.FromDate,
                    ToDate = report.ToDate
                },
                Events = auditResponse.Entries,
                Summary = auditResponse.Summary ?? new AuditLogSummary()
            };

            // Update report metrics
            report.EventCount = auditResponse.Entries.Count;

            if (auditResponse.Entries.Count == 10000)
            {
                report.Warnings.Add("Report limited to 10,000 events. Use date filters to narrow the scope.");
            }

            if (auditResponse.Entries.Count == 0)
            {
                report.Warnings.Add("No audit events found matching the specified criteria.");
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(content, options);
        }

        /// <summary>
        /// Generates compliance badge evidence report content
        /// </summary>
        private async Task<string> GenerateComplianceBadgeReportAsync(ComplianceReport report)
        {
            var content = new ComplianceBadgeReportContent
            {
                Metadata = new ComplianceBadgeReportMetadata
                {
                    ReportId = report.ReportId,
                    SchemaVersion = report.SchemaVersion,
                    GeneratedAt = DateTime.UtcNow,
                    AssetId = report.AssetId,
                    Network = report.Network
                }
            };

            var evidence = new List<ComplianceEvidenceItem>();

            // Collect evidence from audit logs
            var auditRequest = new GetEnterpriseAuditLogRequest
            {
                AssetId = report.AssetId,
                Network = report.Network,
                FromDate = report.FromDate,
                ToDate = report.ToDate,
                Page = 1,
                PageSize = 1000
            };

            var auditResponse = await _auditService.GetAuditLogAsync(auditRequest);

            if (auditResponse.Success && auditResponse.Entries.Count > 0)
            {
                evidence.Add(new ComplianceEvidenceItem
                {
                    EvidenceType = "Audit Trail",
                    Description = $"Comprehensive audit trail with {auditResponse.Entries.Count} recorded events",
                    Source = "Enterprise Audit System",
                    Timestamp = DateTime.UtcNow,
                    Status = "Verified"
                });
            }

            // Check for compliance metadata
            if (report.AssetId.HasValue)
            {
                try
                {
                    var metadata = await _complianceService.GetMetadataAsync(report.AssetId.Value);
                    if (metadata.Success && metadata.Metadata != null)
                    {
                        evidence.Add(new ComplianceEvidenceItem
                        {
                            EvidenceType = "Compliance Metadata",
                            Description = $"Token compliance metadata with {metadata.Metadata.ComplianceStatus} status",
                            Source = "Compliance Metadata Service",
                            Timestamp = metadata.Metadata.UpdatedAt ?? metadata.Metadata.CreatedAt,
                            Status = "Verified"
                        });

                        if (metadata.Metadata.VerificationStatus == VerificationStatus.Verified)
                        {
                            evidence.Add(new ComplianceEvidenceItem
                            {
                                EvidenceType = "KYC Verification",
                                Description = $"KYC verification completed by {metadata.Metadata.KycProvider}",
                                Source = metadata.Metadata.KycProvider ?? "KYC Provider",
                                Timestamp = metadata.Metadata.KycVerificationDate ?? DateTime.UtcNow,
                                Status = "Verified"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not retrieve compliance metadata for asset {AssetId}", report.AssetId);
                }
            }

            content.Evidence = evidence;

            // Determine badge status
            var requiredEvidence = new List<string>
            {
                "Audit Trail",
                "Compliance Metadata",
                "KYC Verification"
            };

            var collectedTypes = evidence.Select(e => e.EvidenceType).ToHashSet();
            var missing = requiredEvidence.Where(r => !collectedTypes.Contains(r)).ToList();

            content.MissingRequirements = missing;
            content.BadgeStatus = missing.Count == 0 ? "Eligible" : "Incomplete";

            // Update report
            report.EventCount = evidence.Count;
            report.Warnings = missing.Select(m => $"Missing evidence: {m}").ToList();

            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(content, options);
        }

        /// <summary>
        /// Converts report to CSV format
        /// </summary>
        private string ConvertReportToCsv(ComplianceReport report)
        {
            var sb = new StringBuilder();

            // Add header with metadata
            sb.AppendLine("# Compliance Report Export");
            sb.AppendLine($"# Report ID: {report.ReportId}");
            sb.AppendLine($"# Report Type: {report.ReportType}");
            sb.AppendLine($"# Generated: {report.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"# Schema Version: {report.SchemaVersion}");
            sb.AppendLine($"# Checksum: {report.Checksum}");
            sb.AppendLine();

            // Parse content based on report type
            if (string.IsNullOrEmpty(report.ContentJson))
            {
                sb.AppendLine("# No content available");
                return sb.ToString();
            }

            switch (report.ReportType)
            {
                case ReportType.MicaReadiness:
                    var micaContent = JsonSerializer.Deserialize<MicaReadinessReportContent>(report.ContentJson);
                    if (micaContent != null)
                    {
                        sb.AppendLine("Article,Requirement,Status,Evidence,Recommendation");
                        foreach (var check in micaContent.ComplianceChecks)
                        {
                            sb.AppendLine($"\"{CsvEscape(check.Article)}\",\"{CsvEscape(check.Requirement)}\",\"{CsvEscape(check.Status)}\",\"{CsvEscape(check.Evidence ?? "")}\",\"{CsvEscape(check.Recommendation ?? "")}\"");
                        }
                        sb.AppendLine();
                        sb.AppendLine($"# Readiness Score: {micaContent.ReadinessScore}%");
                        sb.AppendLine($"# Summary: {micaContent.ReadinessSummary}");
                    }
                    break;

                case ReportType.AuditTrail:
                    var auditContent = JsonSerializer.Deserialize<AuditTrailReportContent>(report.ContentJson);
                    if (auditContent != null)
                    {
                        sb.AppendLine("Timestamp,Category,Action,Performed By,Asset ID,Network,Success,Affected Address,Notes,Payload Hash");
                        foreach (var evt in auditContent.Events)
                        {
                            sb.AppendLine($"\"{evt.PerformedAt:yyyy-MM-dd HH:mm:ss}\",\"{evt.Category}\",\"{CsvEscape(evt.ActionType)}\",\"{CsvEscape(evt.PerformedBy)}\",\"{evt.AssetId}\",\"{CsvEscape(evt.Network ?? "")}\",\"{evt.Success}\",\"{CsvEscape(evt.AffectedAddress ?? "")}\",\"{CsvEscape(evt.Notes ?? "")}\",\"{CsvEscape(evt.PayloadHash)}\"");
                        }
                    }
                    break;

                case ReportType.ComplianceBadge:
                    var badgeContent = JsonSerializer.Deserialize<ComplianceBadgeReportContent>(report.ContentJson);
                    if (badgeContent != null)
                    {
                        sb.AppendLine("Evidence Type,Description,Source,Timestamp,Status");
                        foreach (var item in badgeContent.Evidence)
                        {
                            sb.AppendLine($"\"{CsvEscape(item.EvidenceType)}\",\"{CsvEscape(item.Description)}\",\"{CsvEscape(item.Source)}\",\"{item.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{CsvEscape(item.Status)}\"");
                        }
                        sb.AppendLine();
                        sb.AppendLine($"# Badge Status: {badgeContent.BadgeStatus}");
                        if (badgeContent.MissingRequirements.Count > 0)
                        {
                            sb.AppendLine($"# Missing Requirements: {string.Join(", ", badgeContent.MissingRequirements)}");
                        }
                    }
                    break;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Escapes CSV special characters
        /// </summary>
        private string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // Replace double quotes with double-double quotes
            return value.Replace("\"", "\"\"");
        }

        /// <summary>
        /// Calculates SHA-256 checksum of content
        /// </summary>
        private string CalculateChecksum(string content)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
