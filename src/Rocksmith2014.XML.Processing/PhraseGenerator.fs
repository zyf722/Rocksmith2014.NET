module Rocksmith2014.XML.Processing.PhraseGenerator

open Rocksmith2014.XML
open Rocksmith2014.XML.Extensions
open System
open Utils

type Inst = InstrumentalArrangement
          
let private maxOfThree defaultValue o1 o2 o3 =
    let v1 = o1 |> Option.defaultValue defaultValue
    let v2 = o2 |> Option.defaultValue defaultValue
    let v3 = o3 |> Option.defaultValue defaultValue

    max v1 v2
    |> max v3

let private minOfThree o1 o2 o3 =
    [ Option.toList o1
      Option.toList o2
      Option.toList o3 ]
    |> List.collect id
    |> List.tryMin

let private findContentEnd (arr: Inst) =
    let note =
        arr.Levels.[0].Notes
        |> ResizeArray.tryLast
        |> Option.map (fun x -> x.Time + x.Sustain)

    let chord =
        arr.Levels.[0].Chords
        |> ResizeArray.tryLast
        |> Option.map (fun x ->
            x.Time
            + if x.HasChordNotes then x.ChordNotes.[0].Sustain else 0)

    let hs =
        arr.Levels.[0].HandShapes
        |> ResizeArray.tryLast
        |> Option.map (fun x -> x.EndTime)

    maxOfThree 0 note chord hs

let private getEndPharseTime (arr: Inst) =
    let oldEndPhrase =
        arr.Phrases
        |> Seq.tryFindIndex (fun x -> x.Name.Equals("END", StringComparison.OrdinalIgnoreCase))
        |> Option.bind (fun index ->        
            arr.PhraseIterations
            |> ResizeArray.tryFind (fun x -> x.PhraseId = index))

    match oldEndPhrase with
    | Some oldEnd ->
        oldEnd.Time
    | None ->
        let noMoreContentTime = findContentEnd arr

        // Use the next beat after the content has ended
        arr.Ebeats
        |> Seq.tryFindIndexBack (fun x -> x.Time >= noMoreContentTime)
        |> Option.defaultValue noMoreContentTime

let private findNextContent (level: Level) time =
    let note =
        level.Notes
        |> ResizeArray.tryFind (fun x -> x.Time >= time)
        |> Option.map (fun x -> x.Time)

    let chord =
        level.Chords
        |> ResizeArray.tryFind (fun x -> x.Time >= time)
        |> Option.map (fun x -> x.Time)

    let handShape =
        level.HandShapes
        |> ResizeArray.tryFind (fun x -> x.StartTime >= time)
        |> Option.map (fun x -> x.StartTime)

    minOfThree note chord handShape

let private getContentStartTime (arr: Inst) =
    findNextContent (findFirstLevelWithContent arr) 0

let private getFirstPhraseTime contentStartTime (arr: Inst) =
    let firstBeatTime = arr.Ebeats.[0].Time

    if firstBeatTime = contentStartTime then
        if contentStartTime = 0 then
            failwith "There is no room for an empty phrase before the arrangement content starts."
        else
            let newBeatTime =
                max 0 (firstBeatTime - 1000)
            let newBeat = Ebeat(newBeatTime, 0s)
            Error (newBeat)
    else
        Ok firstBeatTime

let private addEndPhrase endPharseTime ngSections (arr: Inst) =
    let phraseId = arr.Phrases.Count

    arr.Phrases.Add(Phrase("END", 0uy, PhraseMask.None))
    arr.PhraseIterations.Add(PhraseIteration(endPharseTime, phraseId))
    arr.Sections.Add(Section("noguitar", endPharseTime, ngSections + 1s))

let private erasePhrasesAndSections (arr: Inst) =
    arr.Phrases.Clear()
    arr.PhraseIterations.Clear()
    arr.Sections.Clear()

let private addFirstPhrase firstPhraseTime (arr: Inst) =
    arr.Phrases.Add(Phrase("COUNT", 0uy, PhraseMask.None))

    match firstPhraseTime with
    | Ok firstTime ->
        arr.PhraseIterations.Add(PhraseIteration(firstTime, 0))
    | Error newBeat ->
        arr.Ebeats.Insert(0, newBeat)
        arr.PhraseIterations.Add(PhraseIteration(newBeat.Time, 0))

let private findGoodPhraseTime (level: Level) initialTime =
    let outsideNoteSustainTime =
        level.Notes
        |> ResizeArray.tryFind (fun x -> x.Time < initialTime && x.Time + x.Sustain > initialTime)
        |> Option.map (fun x ->
            if initialTime - x.Time < x.Time + x.Sustain - initialTime then
                x.Time
            else
                x.Time + x.Sustain)

    let outsideHandShapeTime =
        level.HandShapes
        |> ResizeArray.tryFind (fun x -> x.StartTime < initialTime && x.EndTime > initialTime)
        |> Option.map (fun x -> x.StartTime)

    outsideNoteSustainTime
    |> Option.orElse outsideHandShapeTime
    |> Option.defaultValue initialTime

let private findActiveAnchor (level: Level) time =
    match level.Anchors.FindLast(fun x -> x.Time <= time) with
    | null -> level.Anchors.[0]
    | anchor -> anchor

let private createPhrasesAndSections contentStartTime endPharseTime (arr: Inst) =
    let mutable riffNumber = 1s
    let mutable ngSectionNumber = 0s
    let mutable phraseNumber = 0s
    let mutable measureCounter = 0
    let mutable nextPhraseTime : int option = None
    let level = arr.Levels.[0]

    // Add first phrase/section at content start time
    arr.Phrases.Add(Phrase($"p{ngSectionNumber}", 0uy, PhraseMask.None))
    arr.PhraseIterations.Add(PhraseIteration(contentStartTime, arr.Phrases.Count - 1))
    arr.Sections.Add(Section("riff", contentStartTime, riffNumber))

    arr.Ebeats
    |> Seq.skipWhile (fun x -> x.Time <= contentStartTime)
    |> Seq.takeWhile (fun x -> x.Time < endPharseTime)
    |> Seq.iter (fun beat ->
        if beat.Measure <> -1s && nextPhraseTime.IsNone then
            measureCounter <- measureCounter + 1

        if measureCounter >= 9 || nextPhraseTime |> Option.exists (fun x -> beat.Time >= x) then
            measureCounter <- 1
            let time =
                match nextPhraseTime with
                | None ->
                    findGoodPhraseTime level beat.Time
                | Some t ->
                    nextPhraseTime <- None
                    t

            // Don't create duplicate phrases (could happen when there is handshape longer than 8 measures)
            if time <> arr.Sections.[arr.Sections.Count - 1].Time then
                let nextContentTime = findNextContent level time

                let isNoGuitar =
                    match nextContentTime with
                    | None ->
                        true
                    | Some nextContentTime ->
                        if nextContentTime - time >= 2500 then
                            nextPhraseTime <- Some nextContentTime
                            true
                        else
                            false

                // Check if a new anchor needs to be created
                let activeAnchor = findActiveAnchor level time
                if activeAnchor.Time <> time then
                    level.Anchors.InsertByTime(Anchor(activeAnchor.Fret, time, activeAnchor.Width))

                phraseNumber <- phraseNumber + 1s
                arr.Phrases.Add(Phrase($"p{phraseNumber}", 0uy, PhraseMask.None))
                arr.PhraseIterations.Add(PhraseIteration(time, arr.Phrases.Count - 1))

                let name, number =
                    if isNoGuitar then
                        ngSectionNumber <- ngSectionNumber + 1s
                        "noguitar", ngSectionNumber
                    else
                        riffNumber <- riffNumber + 1s
                        "riff", riffNumber
                arr.Sections.Add(Section(name, time, number)))

    ngSectionNumber

let generate (arr: Inst) =
    match getContentStartTime arr with
    | Some contentStartTime ->
        let endPharseTime = getEndPharseTime arr
        let firstPhraseTime = getFirstPhraseTime contentStartTime arr

        erasePhrasesAndSections arr
        addFirstPhrase firstPhraseTime arr
        let ngSections = createPhrasesAndSections contentStartTime endPharseTime arr
        addEndPhrase endPharseTime ngSections arr
    | None ->
        // Edge case: there are no notes, chords or handshapes in the arrangement
        ()
