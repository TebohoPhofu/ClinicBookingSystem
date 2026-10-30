// Every test downloads the full Blazor WASM payload; running them in parallel starves slower machines and causes page-load timeouts.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
