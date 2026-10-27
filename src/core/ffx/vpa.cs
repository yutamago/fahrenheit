// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.FFX;

/// <summary>
///     Which characters a walkmesh triangle stops (Sg_IdMatchChr). Groups 3-12 and 15-47 stop nobody.
/// </summary>
public enum VpaTriCollisionGroup {
    Pass         = 0,
    BlockAll     = 1,
    BlockNPC     = 2,  // every character but the player
    BlockChocobo = 13, // characters marked as a chocobo (Ch_SetChocobo: Actor +0x198 bit 2)
    BlockPlayer  = 14,
    // 0x30-0x3F are indirect: the group is replaced by g_ChrIdMapTable[group] (0xF019D8), which event scripts set
    // through native 0x4017 (Sg_IdSetMapTable), and the result is checked again.
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VpaTri {
    public  InlineArray3<short> vertex_indices;
    public  InlineArray3<short> neighbour_indices;
    private int                 data;

    public readonly VpaVertex* vertex_by_index   (int idx) => Globals.Map.vertices + vertex_indices   [idx];
    public readonly VpaTri*    neighbour_by_index(int idx) => Globals.Map.tris     + neighbour_indices[idx];

    public readonly VpaVertex* vert_a { get { return Globals.Map.vertices + vertex_indices[0]; } }
    public readonly VpaVertex* vert_b { get { return Globals.Map.vertices + vertex_indices[1]; } }
    public readonly VpaVertex* vert_c { get { return Globals.Map.vertices + vertex_indices[2]; } }

    public readonly VpaTri* neighbour_a { get { return Globals.Map.tris + neighbour_indices[0]; } }
    public readonly VpaTri* neighbour_b { get { return Globals.Map.tris + neighbour_indices[1]; } }
    public readonly VpaTri* neighbour_c { get { return Globals.Map.tris + neighbour_indices[2]; } }

    // The game reads these fields through the Ch_IdGet* functions, from the copy of this word at Actor +0x828.
    public VpaTriCollisionGroup collision_group { readonly get { return (VpaTriCollisionGroup)data.get_bits(0, 7); } set { data.set_bits(0,  7,  (int)value); } }
    public int                  battle_zone     { readonly get { return data.get_bits(7,  2);                      } set { data.set_bits(7,  2,  value);      } } // Ch_IdGetEncount
    public int                  effect          { readonly get { return data.get_bits(9,  2);                      } set { data.set_bits(9,  2,  value);      } } // Ch_IdGetEffect: index into the current effect table set
    public int                  location        { readonly get { return data.get_bits(11, 2);                      } set { data.set_bits(11, 2,  value);      } } // Ch_IdGetDictionary
    public int                  water           { readonly get { return data.get_bits(13, 2);                      } set { data.set_bits(13, 2,  value);      } } // Ch_IdGetWater
    public int                  sound_type      { readonly get { return data.get_bits(15, 2);                      } set { data.set_bits(15, 2,  value);      } } // Ch_IdGetSound
    public int                  upper           { readonly get { return data.get_bits(17, 15);                     } set { data.set_bits(17, 15, value);      } } // the shade, read through Sg_IdGetShade

    public readonly int shade_0 => upper.get_bits(0,  5); // Ch_IdGetShade0
    public readonly int shade_1 => upper.get_bits(5,  5); // Ch_IdGetShade1
    public readonly int shade_2 => upper.get_bits(10, 5); // Ch_IdGetShade2
}

public struct VpaVertex {
    public  short x;
    public  short y;
    public  short z;
    private short unused;
}

[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 0x20)]
public unsafe struct VpaNavMesh {
    [FieldOffset(0x0A)] public ushort vertex_count;
    [FieldOffset(0x0C)] public float  scale;
    [FieldOffset(0x18)] public uint   vertices_offset;
    [FieldOffset(0x1C)] public uint   tri_info_offset;
}
