using JetBrains.Annotations;
// ReSharper disable EnumUnderlyingTypeIsInt

namespace Terminal.Gui.Analyzers.Internal.Tests.Generators.EnumWrapper32.Prototypes;

// Notes about the enum in this file that I don't want to show up in the generated code:
// This enum has a mix of concepts for the generator to handle when parsing the code.
// 
// This one is, though silly, a mostly well-formed flags enum and demonstrates a wide range of common cases as well
// as some intentionally ridiculous definitions, to prove the generator doesn't care.
// The only "bad" thing about this one is that multiple names exist for the same binary value.
// Those are expected to work for string to struct parsing, but only one will be used for struct to string formatting
// unless I get ambitious and allow an override via a partial method definition or something like that.
// Aside from that, here are other specific notes about what there is to deal with in this enum, including both normal
// as well as goofy/abnormal but legal code, to ensure the generator can handle it all.
// The list is re-used for other test enums, so is given as a checklist.
// Not every possible combination of these things will be demonstrated because that would take billions of years.
//
//  - [ ] Has optional suppression attribute on type
//  - [ ] Has optional configuration attribute on type
//  - [ ] Has optional suppression attribute on any members
//  - [ ] Has optional configuration attribute on any members
//  - [X] Has Flags attribute on type
//  - [X] Has any other attributes on type
//  - [ ] Has any attributes on any members
//  - [X] Has explicit None member
//    - [X] Explicit None value is 0
//  - [ ] Has explicit All member
//    - [ ] Explicit None value is the bitwise combination of all other members
//  - [X] Explicitly declares its backing type
//    - int, even though that's default anyway
//  - [X] Is signed
//  - [X] Has a value trying to set the sign bit
//  - [X] Has single-bit members
//  - [ ] Has multi-bit members/named combinations
//  - [ ] Has members defined as the result of bitwise OR results of other members
//  - [ ] Has members defined as the result of bitwise AND results of other members
//  - [ ] Has an all-flags member
//  - [ ] Has a saturated member (illegal unless all bits are in use)
//  - [X] Has multiple members with the same compile-time constant values
//  - [X] Has XmlDoc
//    - [X] Has summary on type
//    - [X] Has remarks on type
//    - [ ] Has other tags on type
//    - [X] Has summary on any members
//    - [ ] Has other tags on any members
//  - [X] Has implicitly-valued members
//  - [X] Has explicitly-valued members
//  - [X] Has shifted values
//  - [X] Has negated values
//  - [X] Has masked values
//  - [X] Has values referencing another constant
//  - [X] Has signed decimal literals
//  - [X] Has unsigned decimal literals
//    - With int cast, as necessary
//  - [X] Has hex literals
//    - In same variants as decimal, as well as a bitwise negated assignment case for the sign bit
//  - [X] Has binary literals
//    - One unsigned with an int cast, a leading zero, and all bits to the left of that omitted
//    - One signed binary literal that is completely ridiculous, with the last 9 bits set, the whole thing negated,
//      and applying a lower 10 bit mask to the result of the negation, all to set bit 10

/// <summary>
///     This summary tag contents should be copied from the summary tag of the SEnum type.
/// </summary>
/// <remarks>This is a remarks block that should get copied to the generated struct.</remarks>
[UsedImplicitly]
[Flags]
public enum SEnum : int
{
    /// <summary>No flags set</summary>
    None = 0,

    /// <summary>Flag 1 set implicitly (should be 0x1)</summary>
    One,

    /// <summary>Flag 2 set implicitly (should be 0x2)</summary>
    Two,

    /// <summary>Flag 3 set as bare signed decimal literal (4)</summary>
    Three = 4,

    /// <summary>Flag 4 set as bare signed decimal literal (8)</summary>
    Four = 8,

    /// <summary>Flag 6 set as signed hex literal (0x20)</summary>
    Six = 0x20,

    /// <summary>Flag 7 set as unsigned binary literal (0b_01000000u) with an explicit int cast</summary>
    Seven = (int)0b_01000000u,

    /// <summary>Flag 8 set as Flag 7 shifted left by 1 (Seven &lt;&lt; 1)</summary>
    Eight = Seven << 1,

    /// <summary>Flag 9 set as explicit unsigned decimal literal (256U)</summary>
    Nine = (int)256U,

    /// <summary>Flag 10 set as a binary literal which is negated and then masked just to set the 10th bit like some kind of demon</summary>
    Ten = ~0b01_11111111 & 0b1111111111,

    /// <summary>Flag 32 set as signed hex literal (-0x80000000)</summary>
    ThirtyTwo1_SignedHex = -0x80000000,
    /// <summary>Flag 32 set as a negated signed hex literal (~0x7FFFFFFF)</summary>
    ThirtyTwo2_NegatedHex = ~0x7FFFFFFF,
    /// <summary>Flag 32 set as Flag 1 shifted left to the sign bit (One <remarks> 31</remarks>)</summary>
    ThirtyTwo3_ShiftedBit = One << 31,
    /// <summary>Flag 32 set as signed hex literal (-0x80000000)</summary>
    ThirtyTwo4_MinValueConstant = int.MinValue
}