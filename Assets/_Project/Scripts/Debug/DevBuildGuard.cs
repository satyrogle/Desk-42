// DevBuildGuard.cs
// All debug/CLI tooling wraps in this guard.
// Do not ship without this.

#if UNITY_EDITOR || DEVELOPMENT_BUILD

// Desk42CLI and EntropyManager.SanityOverride go here.

#endif
