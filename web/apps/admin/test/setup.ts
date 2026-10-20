// Browser APIs jsdom lacks that Radix components use (the checkbox measures itself with ResizeObserver).
class NoResizeObserver {
  observe() {}
  unobserve() {}
  disconnect() {}
}

globalThis.ResizeObserver ??= NoResizeObserver;
