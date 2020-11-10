﻿module AttributesTests

open Expecto
open System
open Rocksmith2014.SNG
open Rocksmith2014.DLCProject
open Rocksmith2014.DLCProject.Manifest.AttributesCreation
open Rocksmith2014.XML
open Rocksmith2014.Conversion
open Rocksmith2014.Common

let testProject =
    { Version = "1.0"
      DLCKey = "SomeTest"
      ArtistName = SortableString.Create "Artist"
      JapaneseArtistName = None
      JapaneseTitle = None
      Title = SortableString.Create "Title"
      AlbumName = SortableString.Create "Album"
      Year = 2020
      AlbumArtFile = "cover.dds"
      AudioFile = { Path = "audio.wem"; Volume = 1. }
      AudioPreviewFile = { Path = "audio_preview.wem"; Volume = 1. }
      Arrangements = []
      Tones = [] }

let testArr = InstrumentalArrangement.Load("instrumental.xml")
let testSng = ConvertInstrumental.xmlToSng testArr

let testLead =
    { XML = "instrumental.xml"
      Name = ArrangementName.Lead
      RouteMask = RouteMask.Lead
      Priority = ArrangementPriority.Main
      TuningPitch = 440.
      Tuning = [|0s;0s;0s;0s;0s;0s|]
      BaseTone = "Base_Tone"
      Tones = []
      ScrollSpeed = 1.3
      BassPicked = false
      MasterID = 12345
      PersistentID = Guid.NewGuid() }

[<Tests>]
let attributeTests =
  testList "Attribute Tests" [

    testCase "Partition is set correctly" <| fun _ ->
        let lead2 = { testLead with MasterID = 12346; PersistentID = Guid.NewGuid() }

        let project = { testProject with Arrangements = [ Instrumental testLead; Instrumental lead2 ] }

        let attr1 = createAttributes project (FromInstrumental (testLead, testSng))
        let attr2 = createAttributes project (FromInstrumental (lead2, testSng))

        Expect.equal attr1.SongPartition (Nullable(1)) "Partition for first lead arrangement is 1"
        Expect.equal attr2.SongPartition (Nullable(2)) "Partition for second lead arrangement is 2"
        Expect.equal attr2.SongAsset "urn:application:musicgame-song:sometest_lead2" "Song asset is correct"

    testCase "Chord templates are created" <| fun _ ->
        let project = { testProject with Arrangements = [ Instrumental testLead ] }
        let emptyNameId = testSng.Chords |> Array.findIndex (fun c -> String.IsNullOrEmpty c.Name)

        let attr = createAttributes project (FromInstrumental (testLead, testSng))

        Expect.isNonEmpty attr.ChordTemplates "Chord templates array is not empty"
        Expect.isFalse (attr.ChordTemplates |> Array.exists (fun (c: Manifest.ChordTemplate) -> c.ChordId = int16 emptyNameId)) "Chord template with empty name is removed"

    testCase "Sections are created" <| fun _ ->
        let project = { testProject with Arrangements = [ Instrumental testLead ] }

        let attr = createAttributes project (FromInstrumental (testLead, testSng))

        Expect.equal attr.Sections.Length testSng.Sections.Length "Section count is same"
        Expect.equal attr.Sections.[0].UIName "$[34298] Riff [1]" "UI name is correct"

    testCase "Phrases are created" <| fun _ ->
        let project = { testProject with Arrangements = [ Instrumental testLead ] }

        let attr = createAttributes project (FromInstrumental (testLead, testSng))

        Expect.equal attr.Phrases.Length testSng.Phrases.Length "Phrase count is same"

    testCase "Chords are created" <| fun _ ->
        let project = { testProject with Arrangements = [ Instrumental testLead ] }

        let attr = createAttributes project (FromInstrumental (testLead, testSng))

        Expect.isNonEmpty attr.Chords "Chords map is not empty"

    testCase "Techniques are created" <| fun _ ->
        let project = { testProject with Arrangements = [ Instrumental testLead ] }

        let attr = createAttributes project (FromInstrumental (testLead, testSng))

        Expect.isNonEmpty attr.Techniques "Technique map is not empty"

    testCase "Arrangement properties are set" <| fun _ ->
        let project = { testProject with Arrangements = [ Instrumental testLead ] }

        let attr = createAttributes project (FromInstrumental (testLead, testSng))

        match attr.ArrangementProperties with
        | Some ap ->
            Expect.equal ap.standardTuning 1uy "Standard tuning is set"
            Expect.equal ap.openChords 1uy "Open chords is set"
            Expect.equal ap.unpitchedSlides 1uy "Unpitched slides is set"
        | None -> failwith "Arrangement properties do not exist"

    testCase "DNA riffs is set" <| fun _ ->
        let project = { testProject with Arrangements = [ Instrumental testLead ] }

        let attr = createAttributes project (FromInstrumental (testLead, testSng))

        Expect.isGreaterThan attr.DNA_Riffs.Value 0. "DNA riffs is greater than zero"

    testCase "Tone names are set" <| fun _ ->
        let project = { testProject with Arrangements = [ Instrumental testLead ] }

        let attr = createAttributes project (FromInstrumental (testLead, testSng))

        Expect.equal attr.Tone_Base "Base_Tone" "Base tone name is correct"
        Expect.equal attr.Tone_A "Tone_1" "Tone A name is correct"
        Expect.equal attr.Tone_B "Tone_2" "Tone B name is correct"
        Expect.equal attr.Tone_C "Tone_3" "Tone C name is correct"

    testCase "URN attributes are correct" <| fun _ ->
        let project = { testProject with Arrangements = [ Instrumental testLead ] }

        let attr = createAttributes project (FromInstrumental (testLead, testSng))

        Expect.equal attr.AlbumArt "urn:image:dds:album_sometest" "AlbumArt is correct"
        Expect.equal attr.ManifestUrn "urn:database:json-db:sometest_lead" "ManifestUrn is correct"
        Expect.equal attr.BlockAsset "urn:emergent-world:sometest" "BlockAsset is correct"
        Expect.equal attr.ShowlightsXML "urn:application:xml:sometest_showlights" "ShowlightsXML is correct"
        Expect.equal attr.SongAsset "urn:application:musicgame-song:sometest_lead" "SongAsset is correct"
        Expect.equal attr.SongXml "urn:application:xml:sometest_lead" "SongXml is correct"

    testCase "Various attributes are correct (Instrumental)" <| fun _ ->
        let project = { testProject with Arrangements = [ Instrumental testLead ] }

        let attr = createAttributes project (FromInstrumental (testLead, testSng))

        Expect.equal attr.MasterID_RDV 12345 "MasterID is correct"
        Expect.equal attr.FullName "SomeTest_Lead" "FullName is correct"
        Expect.equal attr.PreviewBankPath "song_sometest_preview.bnk" "PreviewBankPath is correct"
        Expect.equal attr.SongBank "song_sometest.bnk" "SongBank is correct"
        Expect.equal attr.SongEvent "Play_SomeTest" "SongEvent is correct"
  ]
