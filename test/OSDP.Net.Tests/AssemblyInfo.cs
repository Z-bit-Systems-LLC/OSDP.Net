using NUnit.Framework;

// Fixtures run in parallel; tests within a fixture share instance state and stay sequential.
// Integration tests use in-memory loopback connections, so fixtures don't contend for ports.
[assembly: Parallelizable(ParallelScope.Fixtures)]
