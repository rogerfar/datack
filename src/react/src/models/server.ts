export type Server = {
    serverId: string;
    name: string;
    dbSettings: ServerDbSettings;
    settings: ServerSettings;
};

export type ServerDbSettings = {
    server: string;
    userName: string;
    password: string;
};

export type ServerSettings = {
    tempPath: string;
};