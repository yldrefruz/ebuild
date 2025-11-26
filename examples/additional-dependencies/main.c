#include <stdio.h>
#include <string.h>

void parent_dir(char* path){
    char* last_slash = strrchr(path, '/');
    if(last_slash){
        *last_slash = '\0';
    }
}

void get_runtime_dir(int argc, char** argv,char* buffer, int buffer_size){
    parent_dir(strcpy(buffer, argv[0]));
}


void print_file_content(const char* path){
    FILE* file = fopen(path, "r");
    if(file){
        char line[256];
        while(fgets(line, sizeof(line), file)){
            printf("%s", line);
        }
        fclose(file);
    } else {
        fprintf(stderr, "Failed to open file: %s\n", path);
    }
}



int main(int argc, char** argv)
{
    char path_buffer[1024], example_buffer[1024];
    get_runtime_dir(argc, argv, path_buffer, sizeof(path_buffer));
    printf("Runtime directory: %s\n", path_buffer);
    snprintf(example_buffer, sizeof(example_buffer), "%s/example.txt", path_buffer);
    fprintf(stdout, "Open file at path: %s\n", example_buffer);
    print_file_content(example_buffer);
    

}