#!/bin/bash
./build.sh
mkdir -p output

# you need to call the exe with the 'mono' command to avoid System.TypeLoadException errors
mono ./sim.exe input/listing_0037_single_register_mov > output/listing_0037_single_register_mov.asm
nasm output/listing_0037_single_register_mov.asm
diff -q output/listing_0037_single_register_mov input/listing_0037_single_register_mov

mono ./sim.exe input/listing_0038_many_register_mov > output/listing_0038_many_register_mov.asm
nasm output/listing_0038_many_register_mov.asm
diff -q output/listing_0038_many_register_mov input/listing_0038_many_register_mov

mono ./sim.exe input/listing_0039_more_movs > output/listing_0039_more_movs.asm
nasm output/listing_0039_more_movs.asm
diff -q output/listing_0039_more_movs input/listing_0039_more_movs

mono ./sim.exe input/listing_0041_add_sub_cmp_jnz > output/listing_0041_add_sub_cmp_jnz.asm
nasm output/listing_0041_add_sub_cmp_jnz.asm
diff -q output/listing_0041_add_sub_cmp_jnz input/listing_0041_add_sub_cmp_jnz

mono ./sim.exe -exec input/listing_0043_immediate_movs > output/listing_0043_immediate_movs.txt
diff -q output/listing_0043_immediate_movs.txt input/listing_0043_immediate_movs.txt

mono ./sim.exe -exec input/listing_0044_register_movs > output/listing_0044_register_movs.txt
diff -q output/listing_0044_register_movs.txt input/listing_0044_register_movs.txt

mono sim.exe -exec input/listing_0046_add_sub_cmp > output/listing_0046_add_sub_cmp.txt
diff output/listing_0046_add_sub_cmp.txt input/listing_0046_add_sub_cmp.txt

mono sim.exe -ip -exec input/listing_0048_ip_register > output/listing_0048_ip_register.txt
diff output/listing_0048_ip_register.txt input/listing_0048_ip_register.txt

mono ./sim.exe -ip -exec input/listing_0051_memory_mov > output/listing_0051_memory_mov.txt
diff output/listing_0051_memory_mov.txt input/listing_0051_memory_mov.txt

mono ./sim.exe -ip -exec input/listing_0052_memory_add_loop > output/listing_0052_memory_add_loop.txt
diff output/listing_0052_memory_add_loop.txt input/listing_0052_memory_add_loop.txt

mono ./sim.exe -exec -ip -explainclocks input/listing_0056_estimating_cycles > output/listing_0056_estimating_cycles.txt
diff output/listing_0056_estimating_cycles.txt input/listing_0056_estimating_cycles.txt
