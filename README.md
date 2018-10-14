# 🏖️ AmityUI

A cross-platform desktop GUI back-end for .NET, written in pure C#

This project is currently a work-in-progress.

## Motivation

Traditionally, developing cross-platform graphical applications on .NET has
been tricky to say the least. One would require one or more native libraries
for each supported platform, to interop with a cross-platform backend (itself
another native library with its own idiosyncracies), or to bite the bullet and
use a full engine like Unity or MonoGame.

AmityUI aims to be a step in the direction of fully managed, *multi-platform*
desktop applications: a lightweight wrapper around common OS functionality,
such as window management, input events, basic 2D drawing, and OpenGL context
creation. What's more, there's no native code: only P/invoke and IPC. This
means that, within the supported OS backends, one could potentially run the
same CLR binary on every device and get a good experience.

Amity aims to be a back-end for other rendering libraries to use, rather than
a complete UI framework; more like GLUT than GTK. You can pick whichever
renderer you like (take a look at the ImageSharp and SkiaSharp examples), or
use the drawing primitives if it fits your use case.

## Design goals

Broadly, Amity aims to be *Fast*, *Simple* and *Compatible*. To do this, we'll
aim for the following:

* No native code
* No reflection or code generation
* No heap allocation by frequently-called methods
* Only functionality present on all backends (or easily emulated)
  added to the interface
* No platform implementation details leaking into the interface
* No extraneous functionality; nothing that could be implemented a layer above
  with comparable performance.
