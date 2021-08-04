namespace DLCBuilder

open Elmish
open Avalonia
open Avalonia.FuncUI
open Avalonia.FuncUI.Elmish
open Avalonia.FuncUI.Components.Hosts
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Media
open Avalonia.Platform
open Avalonia.Themes.Fluent
open System
open System.IO
open System.Reactive.Linq
open System.Reactive.Subjects
open System.Reflection
open Microsoft.Extensions.FileProviders
open Rocksmith2014.Common
open ToneCollection

type MainWindow(commandLineArgs: string array) as this =
    inherit HostWindow()

    let autoSaveSubject = new Subject<unit>()

    let shouldAutoSave newState oldState msg =
        match msg with
        | ProjectLoaded _ ->
            false
        | _ ->
            newState.Config.AutoSave &&
            newState.OpenProjectFile.IsSome &&
            newState.Project <> oldState.Project

    do
        let embeddedProvider = EmbeddedFileProvider(Assembly.GetExecutingAssembly())
        use iconData = embeddedProvider.GetFileInfo("Assets/icon.ico").CreateReadStream()
        base.Icon <- WindowIcon(iconData)
        base.Title <- "Rocksmith 2014 DLC Builder"
        //base.TransparencyLevelHint <- WindowTransparencyLevel.AcrylicBlur
        //base.Background <- Brushes.Transparent
        //base.ExtendClientAreaToDecorationsHint <- true
        //base.ExtendClientAreaChromeHints <- ExtendClientAreaChromeHints.PreferSystemChrome
        //base.ExtendClientAreaTitleBarHeightHint <- -1.0
        base.Width <- 1150.0
        //base.Height <- 850.0
        base.MinWidth <- 970.0
        base.MinHeight <- 700.0

        let hotKeysSub _initialModel = Cmd.ofSub (HotKeys.handleEvent >> this.KeyDown.Add)

        let autoSaveSub _ =
            let sub dispatch =
                autoSaveSubject
                    .Throttle(TimeSpan.FromSeconds 1.)
                    .Add(fun () -> dispatch AutoSaveProject)
            Cmd.ofSub sub

        let progressReportingSub _ =
            let sub dispatch =
                let dispatchProgress task progress = TaskProgressChanged(task, progress) |> dispatch
                Main.arrangementCheckProgress.ProgressChanged.Add(dispatchProgress ArrangementCheckAll)
                Main.psarcImportProgress.ProgressChanged.Add(dispatchProgress PsarcImport)
                Tools.psarcUnpackProgress.ProgressChanged.Add(dispatchProgress PsarcUnpack)
                StateUtils.packageBuildProgress.ProgressChanged.Add(dispatchProgress BuildPackage)
            Cmd.ofSub sub

        //this.VisualRoot.VisualRoot.Renderer.DrawFps <- true
        //this.VisualRoot.VisualRoot.Renderer.DrawDirtyRects <- true

        let view' = Views.Main.view this

        // Add an exception handler to the update function
        let update' msg state =
            try
                let newState, cmd =
                    match msg with
                    | ShowDialog dialog ->
                        state, Dialogs.showDialog dialog state
                    | _ ->
                        Main.update msg state
                if shouldAutoSave newState state msg then autoSaveSubject.OnNext()

                // Workaround for focus issues when opening / closing overlays
                match state, newState with
                | { Overlay = NoOverlay }, { Overlay = overlay } when overlay <> NoOverlay ->
                    FocusHelper.storeFocusedElement()
                | { Overlay = overlay }, { Overlay = NoOverlay } when overlay <> NoOverlay ->
                    FocusHelper.restoreRootFocus()
                | _ ->
                    ()

                newState, cmd
            with ex ->
                // Close the DB connection in case of an unexpected error
                match state.Overlay with
                | ToneCollection cs ->
                    CollectionState.disposeCollection cs.ActiveCollection
                | _ ->
                    ()

                let errorMessage =
                    $"Unhandled exception in the update function.\nMessage: {msg}\nException: {ex.Message}"
                let newState =
                    { state with StatusMessages = List.empty
                                 RunningTasks = Set.empty
                                 Overlay = ErrorMessage(errorMessage, Option.ofString ex.StackTrace) }
                newState, Cmd.none

        let databaseConnector =
            let appDataTones = Path.Combine(Configuration.appDataFolder, "tones")
            Database.createConnector
                (OfficialDataBasePath <| Path.Combine(appDataTones, "official.db"))
                (UserDataBasePath <| Path.Combine(appDataTones, "user.db"))

        let init' = Main.init (AvaloniaBitmapLoader.createInterface()) databaseConnector

        Program.mkProgram init' update' view'
        |> Program.withHost this
        |> Program.withSubscription hotKeysSub
        |> Program.withSubscription progressReportingSub
        |> Program.withSubscription autoSaveSub
        #if DEBUG
        |> Program.withTrace (fun msg _state -> Diagnostics.Debug.WriteLine msg)
        #endif
        |> Program.runWith commandLineArgs

type App() =
    inherit Application()

    let deleteExitCheckFile _ =
        if File.Exists Configuration.exitCheckFilePath then
            File.Delete Configuration.exitCheckFilePath

    override this.Initialize() =
        this.Styles.Add (FluentTheme(baseUri = null, Mode = FluentThemeMode.Dark))
        this.Styles.Load "avares://DLCBuilder/Styles.xaml"
        this.Name <- "Rocksmith 2014 DLC Builder"

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            // Delete the exit check file to indicate that the application exited normally
            desktopLifetime.Exit.Add deleteExitCheckFile

            desktopLifetime.MainWindow <- MainWindow(desktopLifetime.Args)
            base.OnFrameworkInitializationCompleted()
        | _ ->
            ()

module Program =
    [<EntryPoint>]
    let main(args: string[]) =
        // Set up logging for unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException.Add <| fun args ->
            let logMessage =
                match args.ExceptionObject with
                | :? Exception as e ->
                    let baseInfo = $"Unhandled exception ({DateTime.Now})\n{e.GetType().Name}\nMessage: {e.Message}\nSource: {e.Source}\nTarget Site: {e.TargetSite}\nStack Trace:\n{e.StackTrace}"
                    if notNull e.InnerException then
                        let inner = e.InnerException
                        $"{baseInfo}\n\nInner Exception:\nMessage:{inner.Message}\nSource: {inner.Source}\nTarget Site: {inner.TargetSite}\nStack Trace:\n{inner.StackTrace}"
                    else
                        baseInfo
                | unknown ->
                    $"Unknown exception object: {unknown}"
            let path = Path.Combine(Configuration.appDataFolder, "crash_log.txt")
            File.WriteAllText(path, logMessage)

        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
