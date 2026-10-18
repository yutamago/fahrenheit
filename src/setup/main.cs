// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.Tools.Setup;

/* This is the same SDL3 + OpenGL3 + Dear ImGui program stub as the Mod Manager's (see src/modmgr/main.cs),
 * reduced to a single fixed window. To edit the UI, see `ui.cs`.
 */

internal sealed unsafe partial class Program {

    // SDL3
    private static SDLWindow*   _sdl_window;
    private static uint         _sdl_window_id;
    private static SDLGLContext _sdl_context;

    // OGL3
    private static GL?          _gl_context;

    private static int Main(string[] args) {
        FhSetup setup = new(FhSetupPlatform.create(), AppContext.BaseDirectory);

        if (args.Length > 0) {
            if (OperatingSystem.IsWindows()) _attach_parent_console();
            return FhSetupCli.run(setup, args);
        }

        setup.detect();
        return _run_window(setup);
    }

    private static int _run_window(FhSetup setup) {
        if (!SDL.Init(SDLInitFlags.Video | SDLInitFlags.Events)) {
            Console.WriteLine($"Fault in SDL_Init() - {SDL.GetErrorS()}");
            return 1;
        }

        SDL.GLSetAttribute(SDLGLAttr.Doublebuffer, 1);
        SDL.GLSetAttribute(SDLGLAttr.DepthSize,    24);
        SDL.GLSetAttribute(SDLGLAttr.StencilSize,  8);

        uint  main_display_id    = SDL.GetPrimaryDisplay();
        float main_display_scale = SDL.GetDisplayContentScale(main_display_id);
        if (main_display_scale <= 0f) main_display_scale = 1f;

        SDLWindowFlags window_flags =
            SDLWindowFlags.Opengl
          | SDLWindowFlags.Resizable
          | SDLWindowFlags.Hidden
          | SDLWindowFlags.HighPixelDensity;

        _sdl_window = SDL.CreateWindow(
            "Fahrenheit Setup",
            int.CreateChecked(MathF.Round(820 * main_display_scale)),
            int.CreateChecked(MathF.Round(760 * main_display_scale)),
            window_flags);

        if (_sdl_window == null) {
            Console.WriteLine($"Fault in SDL_CreateWindow() - {SDL.GetErrorS()}");
            return 1;
        }

        _sdl_window_id = SDL.GetWindowID    (_sdl_window);
        _sdl_context   = SDL.GLCreateContext(_sdl_window);

        if (_sdl_context == SDLGLContext.Null) {
            Console.WriteLine($"Fault in SDL_GL_CreateContext() - {SDL.GetErrorS()}");
            return 1;
        }

        SDL.GLMakeCurrent    (_sdl_window, _sdl_context);
        SDL.GLSetSwapInterval(1); // Enable VSync
        SDL.SetWindowPosition(_sdl_window,
            (int)SDL.SDL_WINDOWPOS_CENTERED_MASK,
            (int)SDL.SDL_WINDOWPOS_CENTERED_MASK);
        SDL.ShowWindow       (_sdl_window);

        ImGuiContextPtr ctx   = ImGui.CreateContext();
        ImGuiIOPtr      io    = ImGui.GetIO();
        ImGuiStylePtr   style = ImGui.GetStyle();

        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad; // Steam Deck users may have no keyboard at hand.
        io.IniFilename  = null;                               // Do not leave an imgui.ini next to the tool.

        FhSetupUi.apply_style(style);
        style.ScaleAllSizes(main_display_scale);
        style.FontScaleDpi = main_display_scale;
        io.ConfigDpiScaleFonts = true;

        // The runtime ships the Noto Sans font; it reads much better than ImGui's default bitmap font.
        string font_path = Path.Join(setup.bin_dir, "resources", "fonts", "NotoSans-Regular.ttf");
        if (File.Exists(font_path)) io.Fonts.AddFontFromFileTTF(font_path, 18f);

        ImGuiImplSDL3.SetCurrentContext(ctx);
        ImGuiImplSDL3.InitForOpenGL    (new SDLWindowPtrB((SDLWindowB*)_sdl_window), (void*)_sdl_context.Handle);

        ImGuiImplOpenGL3.SetCurrentContext(ctx);
        ImGuiImplOpenGL3.Init             ((byte*)null);

        _gl_context = new (new FhGlContext(_sdl_window, _sdl_context));

        FhSetupUi ui   = new(setup, _sdl_window);
        bool      quit = false;
        SDLEvent  msg  = default;

        while (!quit) {
            while (SDL.PollEvent(ref msg)) {
                ImGuiImplSDL3.ProcessEvent((SDLEventB*)&msg);

                quit = (SDLEventType)msg.Type switch {
                    SDLEventType.Quit                                                            => true,
                    SDLEventType.WindowCloseRequested when msg.Window.WindowID == _sdl_window_id => true,
                    _                                                                            => false
                };
            }

            if (SDL.GetWindowFlags(_sdl_window).HasFlag(SDLWindowFlags.Minimized)) {
                SDL.Delay(10);
                continue;
            }

            ImGuiImplOpenGL3.NewFrame();
            ImGuiImplSDL3   .NewFrame();
            ImGui           .NewFrame();

            ui.draw();

            ImGui.Render();

            _gl_context.Viewport  (0, 0, int.CreateChecked( io.DisplaySize.X ), int.CreateChecked( io.DisplaySize.Y ));
            _gl_context.ClearColor(0.08F, 0.08F, 0.09F, 1F);
            _gl_context.Clear     (GLClearBufferMask.ColorBufferBit);

            ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

            SDL.GLSwapWindow(_sdl_window);
        }

        ui.Dispose();

        ImGuiImplOpenGL3.Shutdown();
        ImGuiImplSDL3   .Shutdown();

        ImGui.DestroyContext();

        SDL.GLDestroyContext(_sdl_context);
        SDL.DestroyWindow   (_sdl_window);
        SDL.Quit();

        return 0;
    }

    /* The tool is a GUI application on Windows, so it has no console of its own.
     * When started from a terminal with arguments, print to that terminal instead.
     */

    private const uint _attach_parent_process = 0xFFFFFFFF;
    private const int  _std_output_handle     = -11;

    [LibraryImport("kernel32.dll")]
    private static partial nint GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(uint dwProcessId);

    [SupportedOSPlatform("windows")]
    private static void _attach_parent_console() {
        // Output that is already redirected (to a pipe or file) works as is.
        nint handle = GetStdHandle(_std_output_handle);
        if (handle != 0 && handle != -1) return;

        AttachConsole(_attach_parent_process);
    }
}

/* Hexa's GL wrapper needs this intermediary; see the same type in the Mod Manager.
 */

internal sealed unsafe class FhGlContext(SDLWindow* window, SDLGLContext context) : IGLContext {
    public nint Handle    => (nint)window;
    public bool IsCurrent => SDL.GLGetCurrentContext() == context;

    public void Dispose() { }

    public nint GetProcAddress      (string procName)      => (nint)SDL.GLGetProcAddress    (procName);
    public bool IsExtensionSupported(string extensionName) =>       SDL.GLExtensionSupported(extensionName);

    public void MakeCurrent ()             => SDL.GLMakeCurrent(window, context);
    public void SwapBuffers ()             => SDL.GLSwapWindow (window);
    public void SwapInterval(int interval) => SDL.GLSetSwapInterval(interval);

    public bool TryGetProcAddress(string procName, out nint procAddress) {
        return (procAddress = (nint)SDL.GLGetProcAddress(procName)) != 0;
    }
}
