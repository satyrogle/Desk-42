// ============================================================
// DESK 42 — assembly metadata
//
// Exposes internals to the test assemblies only.
//
// The authored-proof state (EliasProofSessionState) deliberately keeps its
// setters `internal` so only the controller and policies can mutate the
// causal chain. Bucket 2 needs to assert that this state survives a real
// Newtonsoft round-trip, which means constructing a populated instance in a
// test. Widening the setters to public would weaken the invariant for
// production code; InternalsVisibleTo keeps the invariant and grants access
// to the test assemblies alone.
// ============================================================

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Desk42.Tests.EditMode")]
[assembly: InternalsVisibleTo("Desk42.Tests.PlayMode")]
