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

