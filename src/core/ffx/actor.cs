// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.FFX;

[Flags]
public enum ActorFlags : uint {
    KEEP_FPS = 1 << 14,
}

[InlineArray(26)]
public struct ChrPosVectors {
    private Vector4 _e0;
}

[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Auto, Pack = 4, Size = 0x880)]
public unsafe struct Actor {
    [FieldOffset(0x0)]   public ushort     chr_id; // kind << 12 | number
    [FieldOffset(0x2)]   public ushort     chr_enabled;
    [FieldOffset(0x4)]   public char*      chr_name;
    [FieldOffset(0xC)]   public Vector4    chr_pos_vec;
    [FieldOffset(0x1C)]  public Vector4    chr_pos_vec_bkup; // where the character goes this frame; Ch_SetPos copies the position here
    [FieldOffset(0x2C)]  public Vector3    chr_pos_vec_2; // Ch_GetVel2
    [FieldOffset(0x50)]  public float      chr_vel_y; // Ch_SetVelY: the argument * 0.05
    [FieldOffset(0x5C)]  public Vector4    chr_scale_vec;
    [FieldOffset(0x6C)]  public Vector4    chr_offset_vec;
    [FieldOffset(0x7C)]  public Vector3    chr_ground_normal; // (0, -1, 0) after Ch_EnMapID(chr, 0)
    [FieldOffset(0xBC)]  public Vector3    chr_shade_col_effect; // Ch_SetShadeColEffect
    [FieldOffset(0x10C)] public Matrix4x4* chr_org_matrix; // Ch_SetOrgMatrix: the matrix the character is placed by
    [FieldOffset(0x110)] public float      chr_org_matrix_scale; // Ch_SetOrgMatrixScale
    [FieldOffset(0x154)] public float      chr_speed;
    [FieldOffset(0x158)] public float      chr_rotation_rad;
    [FieldOffset(0x168)] public float      chr_direction;
    [FieldOffset(0x16C)] public float      chr_ground_height; // Ch_SetJimen
    [FieldOffset(0x170)] public float      chr_run_anim_spd_threshold;
    [FieldOffset(0x17C)] public float      chr_shadow_height; // Ch_ShadowSetHeight
    [FieldOffset(0x180)] public byte       chr_hide_flags; // Chr_SetHideFlags: 0x1 event, 0x2 effect, 0x8 display, 0x10 battle
    [FieldOffset(0x181)] public byte       chr_hide_calc; // Ch_SetHideCalc
    [FieldOffset(0x182)] public sbyte      chr_grav_mode; // Ch_SetGravMode
    [FieldOffset(0x183)] public sbyte      chr_shadow_mode; // Ch_SetShadowMode
    [FieldOffset(0x184)] public byte       chr_sound_id; // Ch_SoundSetId
    [FieldOffset(0x185)] public sbyte      chr_field_mode; // Ch_SetFieldMode; also the slot of the default motion
    [FieldOffset(0x186)] public byte       chr_motion_type; // Ch_SetMotionType
    [FieldOffset(0x188)] public byte       chr_plight_mask; // Ch_PLightSetEnableMask
    [FieldOffset(0x18C)] public uint       chr_battle_chr_num; // Ch_SetBattleChrNum
    [FieldOffset(0x190)] public uint       chr_bind_id; // copied to a bound child's chr_bind_parent_id
    [FieldOffset(0x194)] public ActorFlags chr_flags;
    [FieldOffset(0x198)] public uint       chr_flags_2; // bit 2 chocobo (Ch_SetChocobo), bit 3 VU1 clip mode (Ch_Vu1SetClipmode)
    [FieldOffset(0x19C)] public Actor*     chr_bind_parent; // Ch_BindToElemDirect
    [FieldOffset(0x1A0)] public uint       chr_bind_elem; // the parent's element (bone) this one is bound to
    [FieldOffset(0x1A4)] public Vector3    chr_bind_offset; // Ch_BindToElemDirectOfs
    [FieldOffset(0x1B4)] public float      chr_bind_scale; // Ch_BindSetScale
    [FieldOffset(0x1B8)] public uint       chr_bind_parent_id; // the parent's chr_bind_id, -1 when unbound
    [FieldOffset(0x1BC)] public void*      chr_models; // 0x38 bytes each (Ch_ModelGetHide, Ch_ModelGetNromPtr)
    [FieldOffset(0x1C0)] public void*      chr_model_header; // Ch_GetNbModel, Ch_GetNbElem
    [FieldOffset(0x1C4)] public void*      chr_blk; // the model block (Ch_GetBlkDirName)
    [FieldOffset(0x328)] public void*      chr_elems; // elements (bones), 0x160 bytes each (Ch_ElemGetParent)
    [FieldOffset(0x330)] public LVec3f     chr_shade_r_vec;
    [FieldOffset(0x33C)] public LVec3f     chr_shade_g_vec;
    [FieldOffset(0x348)] public LVec3f     chr_shade_b_vec;
    [FieldOffset(0x354)] public LVec3f     chr_transparency_vec;
    [FieldOffset(0x360)] public LVec3f     chr_shade_vec; // Ch_SetShade
    [FieldOffset(0x36C)] public LVec3f     chr_light_value_vec; // Ch_GetLightValue
    [FieldOffset(0x378)] public LVec3f     chr_specular_alpha_vec; // Ch_SetSpecularAlpha
    [FieldOffset(0x408)] public uint       chr_effect_level; // Ch_EffectSetEffectLevel
    [FieldOffset(0x414)] public float      chr_neck_mot_target_pct;
    [FieldOffset(0x418)] public float      chr_neck_mot_actual_pct;
    [FieldOffset(0x41C)] public float      chr_neck_facing_target_pct;
    [FieldOffset(0x420)] public float      chr_neck_speed;
    [FieldOffset(0x424)] public float      chr_neck_rot_limit_l; // in deg
    [FieldOffset(0x428)] public float      chr_neck_rot_limit_h; // in deg
    [FieldOffset(0x42C)] public Matrix4x4  chr_neck_matrix_0; // battle only during targeting
    [FieldOffset(0x46C)] public Matrix4x4  chr_neck_matrix_1; // battle only during targeting
    [FieldOffset(0x4D4)] public sbyte      chr_mouth_auto_talk; // Ch_MouthSetAutoTalk
    [FieldOffset(0x4E4)] public float      chr_thickness; // Ch_SetThickness: collision radius
    [FieldOffset(0x4EC)] public float      chr_thickness_btl_cam; // Ch_SetThicknessBtlCam
    [FieldOffset(0x4F0)] public float      chr_fix_height; // Ch_SetFixHeight
    [FieldOffset(0x4F4)] public float      chr_cam_height; // Ch_SetCamHeight
    [FieldOffset(0x4F8)] public float      chr_hit_height; // Ch_SetHitHeight
    [FieldOffset(0x500)] public float      chr_weight; // Ch_SetWeight: used when characters push each other
    [FieldOffset(0x504)] public float      chr_grav; // Ch_AddGrav, Ch_GetGrav
    [FieldOffset(0x51C)] public float      chr_hiju; // Ch_SetHiju
    // ? character sub-positions (limbs et al.)
    // Index 23 is the bottom (Ch_OptposGetBottom), 24 the top (Ch_OptposGetTop), 25 the camera reference (Ch_OptposGetCameraRef).
    [FieldOffset(0x524)] public ChrPosVectors chr_optpos;
    [FieldOffset(0x6D8)] public uint          chr_shadow_dir_h; // Ch_SetShadowDirH
    [FieldOffset(0x6E4)] public void*         chr_clut_textures; // 0x40 bytes each (Ch_TextureGetClutAddr)
    [FieldOffset(0x6E8)] public short         chr_clut_texture_count;
    [FieldOffset(0x6EC)] public void*         chr_image_textures; // 0x40 bytes each (Ch_TextureGetImageAddr)
    [FieldOffset(0x6F0)] public short         chr_image_texture_count;
    [FieldOffset(0x700)] public uint          chr_texture_anim_id; // Ch_TextureGetAnimId
    [FieldOffset(0x704)] public void*         chr_cluts; // 16 bytes each (Ch_ClutGetPtr, Ch_ClutGetSize)
    [FieldOffset(0x726)] public ushort        chr_motion_loop; // Ch_MotionSetLoop
    [FieldOffset(0x72C)] public uint          chr_motion_id; // Ch_MotionGetId
    [FieldOffset(0x73A)] public ushort        chr_motion_hokan; // Ch_MotionSetHokan: interpolation frames into the next clip
    [FieldOffset(0x73C)] public ushort        chr_motion_next_hokan; // Ch_MotionSetNextHokan
    [FieldOffset(0x740)] public uint          chr_motion_frame; // Ch_MotionGetFrame: 8.8 fixed point
    [FieldOffset(0x750)] public short         chr_motion_speed; // Ch_SetMotionSpeed: 8.8 fixed point
    [FieldOffset(0x75C)] public fixed uint    chr_motion_moves[3]; // Ch_MotionSetMoves: stand, walk, run
    [FieldOffset(0x812)] public ushort        chr_cloth_settle_frames; // Ch_ClothSetFast
    [FieldOffset(0x820)] public uint          chr_cloth_flags; // bit 0 gravity (Ch_ClothEnableGrav), bit 2 needs an update
    [FieldOffset(0x824)] public short         chr_tri_idx; // the walkmesh triangle the character stands on, -1 = look it up again
    [FieldOffset(0x828)] public uint          chr_map_id; // the triangle's map id word (Ch_IdGetMap and the other Ch_IdGet* readers)
    [FieldOffset(0x830)] public void*         chr_render_instance; // Ch_GetChrRenderInstance
    [FieldOffset(0x864)] public Vector3       chr_cloth_mesh_offset; // Ch_ClothMeshMappingOffset
}
