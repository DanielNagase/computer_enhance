#!/bin/bash
mcs sim.cs
mkdir -p output

# you need to call the exe with the 'mono' command to avoid System.TypeLoadException errors
mono ./sim.exe listing_0037_single_register_mov > output/listing_0037_single_register_mov.asm
nasm output/listing_0037_single_register_mov.asm
diff -q output/listing_0037_single_register_mov listing_0037_single_register_mov

mono ./sim.exe listing_0038_many_register_mov > output/listing_0038_many_register_mov.asm
nasm output/listing_0038_many_register_mov.asm
diff -q output/listing_0038_many_register_mov listing_0038_many_register_mov

mono ./sim.exe listing_0039_more_movs > output/listing_0039_more_movs.asm
nasm output/listing_0039_more_movs.asm
diff -q output/listing_0039_more_movs listing_0039_more_movs

mono ./sim.exe listing_0041_add_sub_cmp_jnz > output/listing_0041_add_sub_cmp_jnz.asm
nasm output/listing_0041_add_sub_cmp_jnz.asm
diff -q output/listing_0041_add_sub_cmp_jnz listing_0041_add_sub_cmp_jnz
