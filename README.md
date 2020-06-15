# Treedepth

[![License](https://img.shields.io/github/license/PhiliPdB/treedepth-exact)](./LICENSE)
[![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.3866006.svg)](https://doi.org/10.5281/zenodo.3866006)

This software is part of the submission of 'exact' track of [PACE 2020](https://pacechallenge.org/2020/). It is used to compute the exact treedepth of a given graph. This algorithm is also part of my bachelor thesis, which was written at Utrecht University and supervised by Erik Jan van Leeuwen.


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

