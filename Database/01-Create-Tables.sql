create table IgUserNames
(
    IgUserId   bigint       not null,
    IgUserName varchar(128) not null,
    IgFullName varchar(256) null,
    primary key (IgUserId, IgUserName)
);

create table Pictures
(
    PictureId   bigint unsigned auto_increment
        primary key,
    FileName    varchar(256)         not null,
    Caption     varchar(256)         null,
    Description varchar(1024)        null,
    IsDeleted   tinyint(1) default 0 not null,
    IsIncoming  tinyint(1) default 0 not null,
    Clothing    varchar(512)         null,
    Emotions    varchar(512)         null,
    Objects     varchar(512)         null,
    People      varchar(512)         null,
    Race        varchar(512)         null,
    Gender      varchar(512)         null,
    DateAdded   datetime             null
);

ALTER TABLE Pictures ADD UNIQUE (FileName);

ALTER TABLE Pictures
    ADD FULLTEXT INDEX ft_picture_search (
                                          Caption,
                                          Description,
                                          Clothing,
                                          Emotions,
                                          Objects,
                                          People,
                                          Race,
                                          Gender
        );

create table IgMetadata
(
    IgUserId    bigint          not null,
    IgPictureId bigint          not null,
    IgPostId    bigint          null,
    PictureId   bigint unsigned null,
    TakenAt     bigint          not null,
    Caption     varchar(2500)   null,
    ShortCode   varchar(64)     null,
    primary key (IgUserId, IgPictureId),
    constraint FK_IgMetadata_Pictures
        foreign key (PictureId) references Pictures (PictureId)
            on delete cascade
);

create index Pictures_IsDeleted_index
    on Pictures (IsDeleted);

create index Pictures_IsIncoming_index
    on Pictures (IsIncoming desc);

create table User
(
    UserId       char(36)                 not null
        primary key,
    UserName     varchar(255)             not null,
    Email        varchar(255)             not null,
    CreationTime datetime default (now()) null
);

create table PictureViews
(
    ViewId    bigint auto_increment
        primary key,
    UserId    char(36)                           not null,
    PictureId bigint unsigned                    not null,
    DateTime  datetime default CURRENT_TIMESTAMP not null,
    constraint FK_PictureViews_Pictures
        foreign key (PictureId) references Pictures (PictureId)
            on delete cascade,
    constraint FK_PictureViews_User
        foreign key (UserId) references User (UserId)
            on delete cascade
);

create table PictureVotes
(
    UserId        char(36)                           not null,
    PictureId     bigint unsigned                    not null,
    VoteDirection enum ('up', 'down')                not null,
    DateTime      datetime default CURRENT_TIMESTAMP not null,
    IsActive      tinyint(1)                         null,
    primary key (UserId, PictureId),
    constraint FK_PictureVotes_Pictures
        foreign key (PictureId) references Pictures (PictureId)
            on delete cascade,
    constraint FK_PictureVotes_User
        foreign key (UserId) references User (UserId)
            on delete cascade
);

create index IX_PictureVotes_VoteDirection
    on PictureVotes (VoteDirection);

create table UserFavorites
(
    UserId    char(36)                             not null,
    PictureId bigint unsigned                      not null,
    DateTime  datetime   default CURRENT_TIMESTAMP not null,
    IsActive  tinyint(1) default 1                 not null,
    primary key (UserId, PictureId),
    constraint FK_UserFavorites_Pictures
        foreign key (PictureId) references Pictures (PictureId)
            on delete cascade,
    constraint FK_UserFavorites_User
        foreign key (UserId) references User (UserId)
            on delete cascade
);

