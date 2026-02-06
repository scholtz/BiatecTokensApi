using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for MetricsService
    /// </summary>
    [TestFixture]
    public class MetricsServiceTests
    {
        private MetricsService _service = null!;
        private Mock<ILogger<MetricsService>> _mockLogger = null!;

        [SetUp]
        public void SetUp()
        {
            _mockLogger = new Mock<ILogger<MetricsService>>();
            _service = new MetricsService(_mockLogger.Object);
        }

        [Test]
        public void RecordRequest_IncrementsCounter()
        {
            // Act
            _service.RecordRequest("/api/test", "GET", 100.5);

            // Assert
            var metrics = _service.GetMetrics();
            var counters = (Dictionary<string, long>)metrics["counters"];
            
            Assert.That(counters.ContainsKey("http_requests_total.GET.api.test"), Is.True,
                "Counter should exist for the request");
            Assert.That(counters["http_requests_total.GET.api.test"], Is.EqualTo(1),
                "Counter should be incremented once");
        }

        [Test]
        public void RecordRequest_RecordsLatency()
        {
            // Act
            _service.RecordRequest("/api/test", "GET", 150.7);

            // Assert
            var metrics = _service.GetMetrics();
            var histogramsObj = metrics["histograms"];
            
            Assert.That(histogramsObj, Is.Not.Null,
                "Histograms should be present");
            
            // Check if the histogram key exists by converting to string and checking
            var histogramsStr = System.Text.Json.JsonSerializer.Serialize(histogramsObj);
            Assert.That(histogramsStr.Contains("http_request_duration_ms.GET.api.test"), Is.True,
                "Histogram should exist for request duration");
        }

        [Test]
        public void RecordError_IncrementsErrorCounters()
        {
            // Act
            _service.RecordError("/api/test", "POST", "VALIDATION_ERROR");

            // Assert
            var metrics = _service.GetMetrics();
            var counters = (Dictionary<string, long>)metrics["counters"];
            
            Assert.That(counters.ContainsKey("http_errors_total.POST.api.test.VALIDATION_ERROR"), Is.True,
                "Specific error counter should exist");
            Assert.That(counters.ContainsKey("http_errors_by_code.VALIDATION_ERROR"), Is.True,
                "Error by code counter should exist");
        }

        [Test]
        public void RecordDeployment_TracksSuccessAndFailure()
        {
            // Act
            _service.RecordDeployment("ERC20", true, 500.0);
            _service.RecordDeployment("ERC20", false, 300.0);
            _service.RecordDeployment("ERC20", true, 450.0);

            // Assert
            var metrics = _service.GetMetrics();
            var counters = (Dictionary<string, long>)metrics["counters"];
            
            Assert.That(counters["token_deployments_total.ERC20.success"], Is.EqualTo(2),
                "Success counter should be 2");
            Assert.That(counters["token_deployments_total.ERC20.failure"], Is.EqualTo(1),
                "Failure counter should be 1");
        }

        [Test]
        public void RecordDeployment_UpdatesSuccessRateGauge()
        {
            // Act
            _service.RecordDeployment("ASA", true, 100.0);
            _service.RecordDeployment("ASA", true, 100.0);
            _service.RecordDeployment("ASA", false, 100.0);

            // Assert
            var metrics = _service.GetMetrics();
            var gauges = (Dictionary<string, double>)metrics["gauges"];
            
            Assert.That(gauges.ContainsKey("token_deployment_success_rate.ASA"), Is.True,
                "Success rate gauge should exist");
            Assert.That(gauges["token_deployment_success_rate.ASA"], Is.EqualTo(2.0/3.0).Within(0.01),
                "Success rate should be 66.67%");
        }

        [Test]
        public void RecordRpcCall_TracksCallsByNetwork()
        {
            // Act
            _service.RecordRpcCall("algorand", "getStatus", true, 50.0);
            _service.RecordRpcCall("algorand", "getStatus", false, 75.0);

            // Assert
            var metrics = _service.GetMetrics();
            var counters = (Dictionary<string, long>)metrics["counters"];
            
            Assert.That(counters["rpc_calls_total.algorand.getStatus.success"], Is.EqualTo(1),
                "RPC success counter should be 1");
            Assert.That(counters["rpc_calls_total.algorand.getStatus.failure"], Is.EqualTo(1),
                "RPC failure counter should be 1");
        }

        [Test]
        public void RecordRpcCall_UpdatesFailureRateGauge()
        {
            // Act
            _service.RecordRpcCall("evm", "call", true, 50.0);
            _service.RecordRpcCall("evm", "call", true, 50.0);
            _service.RecordRpcCall("evm", "call", false, 50.0);

            // Assert
            var metrics = _service.GetMetrics();
            var gauges = (Dictionary<string, double>)metrics["gauges"];
            
            Assert.That(gauges.ContainsKey("rpc_failure_rate.evm"), Is.True,
                "Failure rate gauge should exist");
            Assert.That(gauges["rpc_failure_rate.evm"], Is.EqualTo(1.0/3.0).Within(0.01),
                "Failure rate should be 33.33%");
        }

        [Test]
        public void RecordAuditWrite_TracksSuccessAndFailure()
        {
            // Act
            _service.RecordAuditWrite("compliance", true);
            _service.RecordAuditWrite("compliance", false);

            // Assert
            var metrics = _service.GetMetrics();
            var counters = (Dictionary<string, long>)metrics["counters"];
            
            Assert.That(counters["audit_writes_total.compliance.success"], Is.EqualTo(1));
            Assert.That(counters["audit_writes_total.compliance.failure"], Is.EqualTo(1));
        }

        [Test]
        public void IncrementCounter_AddsToExistingValue()
        {
            // Act
            _service.IncrementCounter("custom.metric", 5);
            _service.IncrementCounter("custom.metric", 3);

            // Assert
            var metrics = _service.GetMetrics();
            var counters = (Dictionary<string, long>)metrics["counters"];
            
            Assert.That(counters["custom.metric"], Is.EqualTo(8),
                "Counter should accumulate values");
        }

        [Test]
        public void RecordHistogram_StoresValue()
        {
            // Act
            _service.RecordHistogram("custom.latency", 100.5);
            _service.RecordHistogram("custom.latency", 200.3);

            // Assert
            var metrics = _service.GetMetrics();
            var histogramsObj = metrics["histograms"];
            
            Assert.That(histogramsObj, Is.Not.Null,
                "Histograms should be present");
            
            var histogramsStr = System.Text.Json.JsonSerializer.Serialize(histogramsObj);
            Assert.That(histogramsStr.Contains("custom.latency"), Is.True,
                "Histogram should exist");
        }

        [Test]
        public void SetGauge_UpdatesValue()
        {
            // Act
            _service.SetGauge("custom.gauge", 42.5);
            _service.SetGauge("custom.gauge", 100.0);

            // Assert
            var metrics = _service.GetMetrics();
            var gauges = (Dictionary<string, double>)metrics["gauges"];
            
            Assert.That(gauges["custom.gauge"], Is.EqualTo(100.0),
                "Gauge should be updated to latest value");
        }

        [Test]
        public void GetMetrics_ReturnsAllMetricTypes()
        {
            // Act
            _service.IncrementCounter("test.counter");
            _service.RecordHistogram("test.histogram", 50.0);
            _service.SetGauge("test.gauge", 42.0);

            var metrics = _service.GetMetrics();

            // Assert
            Assert.That(metrics.ContainsKey("counters"), Is.True);
            Assert.That(metrics.ContainsKey("histograms"), Is.True);
            Assert.That(metrics.ContainsKey("gauges"), Is.True);
        }
    }
}
