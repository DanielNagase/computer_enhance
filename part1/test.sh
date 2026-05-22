#!/bin/bash
mcs sim.cs
mkdir -p output

./sim.exe listing_0037_single_register_mov > output/listing_0037_single_register_mov.asm
nasm output/listing_0037_single_register_mov.asm
diff -q output/listing_0037_single_register_mov listing_0037_single_register_mov

./sim.exe listing_0038_many_register_mov > output/listing_0038_many_register_mov.asm
nasm output/listing_0038_many_register_mov.asm
diff -q output/listing_0038_many_register_mov listing_0038_many_register_mov

