﻿namespace Rocksmith2014.Common

open System

module Option =
    /// Creates an option from a string, where a null or whitespace string equals None.
    let ofString s = if String.IsNullOrWhiteSpace s then None else Some s

module String =
    /// Returns true if the string is not null or whitespace.
    let notEmpty = (String.IsNullOrWhiteSpace >> not)

module List =
    /// Removes the item at the given index form the list.
    let removeAt index (list: 'a list) =
        let rec remove current list =
            match list with
            | [] -> []
            | _::tail when current = index -> tail
            | h::tail -> h::(remove (current + 1) tail)
    
        remove 0 list

    /// Removes the item from the list.
    let rec remove (item: 'a) (list: 'a list) =
        match list with
        | [] -> []
        | h::tail when h = item -> tail
        | h::tail -> h::(remove item tail)

    /// Switches the old item into the new one in the list.
    let rec update (oldItem: 'a) (newItem: 'a) (list: 'a list) =
        match list with
        | [] -> []
        | h::tail when h = oldItem -> newItem::tail
        | h::tail -> h::(update oldItem newItem tail)
