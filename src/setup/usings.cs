// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

/* Hexa duplicates some definitions for SDL3 structures. We must disambiguate and cast between them.
 * See the same file in the Mod Manager.
 */
global using SDLWindow     = Hexa.NET.SDL3.SDLWindow;
global using SDLEvent      = Hexa.NET.SDL3.SDLEvent;
global using SDLWindowB    = Hexa.NET.ImGui.Backends.SDL3.SDLWindow;
global using SDLEventB     = Hexa.NET.ImGui.Backends.SDL3.SDLEvent;
global using SDLWindowPtrB = Hexa.NET.ImGui.Backends.SDL3.SDLWindowPtr;

// C# stdlib essentials
global using System;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Numerics;
global using System.Runtime.InteropServices;
global using System.Runtime.InteropServices.Marshalling;
global using System.Runtime.Versioning;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;

// ImGui C# binding
global using Hexa.NET.ImGui;

// SDL3/OGL3 typedefs
global using Hexa.NET.ImGui.Backends.OpenGL3;
global using Hexa.NET.ImGui.Backends.SDL3;
global using Hexa.NET.SDL3;
global using Hexa.NET.OpenGL;

global using HexaGen.Runtime;
