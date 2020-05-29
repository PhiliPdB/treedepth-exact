# Treedepth

This software is part of the submission of 'exact' track of [PACE 2020](https://pacechallenge.org/2020/). It is used to compute the exact treedepth of a given graph.


## Installation
The software uses msbuild as build tool. When running on linux, [mono](https://www.mono-project.com/) is required to run the software.
The software can then be build by running
```
make release
```

## Usage
The input graphs are read from the standard input and need to be in the .gr format as specified on the PACE website. It then outputs the treedepth as well a treedepth decomposition in the .tree format to the standard output.

### Example usage
```
mono ./Treedepth.exe < input_graph.gr
```

