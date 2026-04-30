# 5 MCP Tools Every .NET Developer Should Know (From Someone Who's Actually Used Them)

I've been building MCP servers in .NET for a while now — agents that query databases, read files, call APIs, and actually *do things* inside a running system. Along the way, I've accumulated a short list of tools I keep coming back to.

MCP is only as good as the ecosystem around it. The spec is the foundation. But the tools — the servers you connect to, the SDKs you build with, the clients that make it click — that's what actually determines whether your agent does anything useful.

Before I get into the list: I run everything locally against **Ollama** during development. If you haven't set that up yet, it's what everything below runs on — worth doing first. Once you have that running, here's what's actually in my MCP stack.
