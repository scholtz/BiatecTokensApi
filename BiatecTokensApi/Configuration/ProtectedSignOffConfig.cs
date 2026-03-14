namespace BiatecTokensApi.Configuration
{
    /// <summary>
    /// Configuration for the protected sign-off environment.
    ///
    /// <para>
    /// This class defines operator-controlled settings for the backend protected sign-off lane.
    /// Bind it from the <c>ProtectedSignOff</c> section in <c>appsettings.json</c> or from
    /// environment variables using the <c>ProtectedSignOff__</c> prefix.
    /// </para>
    ///
    /// <para>
    /// Required secrets and configuration keys are validated at environment-check time via
    /// <c>POST /api/v1/protected-sign-off/environment/check</c>. When required keys are absent
    /// the check returns <c>Misconfigured</c> status and provides actionable guidance.
    /// </para>
    /// </summary>
    public class ProtectedSignOffConfig
    {
        /// <summary>Configuration section name used for DI binding.</summary>
        public const string SectionName = "ProtectedSignOff";

        /// <summary>
        /// Overrides the default protected sign-off issuer ID
        /// (<c>biatec-protected-sign-off-issuer</c>).
        ///
        /// Set this when the protected tenant uses a custom issuer ID distinct from the
        /// development default. Must be stable across CI runs.
        /// </summary>
        public string? SignOffIssuerId { get; set; }

        /// <summary>
        /// Overrides the default protected sign-off admin user ID
        /// (<c>biatec-sign-off-admin@biatec.io</c>).
        ///
        /// Set this when the protected tenant's admin identity differs from the development
        /// default. Must match an account resolvable via the authentication service.
        /// </summary>
        public string? SignOffAdminUserId { get; set; }

        /// <summary>
        /// When <c>true</c> (default), the environment-readiness check validates that all
        /// required backend configuration keys are present and non-empty. A missing key causes
        /// the check to return <c>Misconfigured</c> status rather than silently degrading.
        ///
        /// Set to <c>false</c> only in isolated unit-test environments where full application
        /// configuration is not available. Never disable in staging or production.
        /// </summary>
        public bool EnforceConfigGuards { get; set; } = true;

        /// <summary>
        /// Optional label identifying the protected environment tier (e.g., "staging",
        /// "protected-ci", "release-candidate").
        ///
        /// Used in diagnostics and log messages to identify which environment produced
        /// the sign-off evidence. Does not affect runtime behaviour.
        /// </summary>
        public string EnvironmentLabel { get; set; } = "default";
    }
}
