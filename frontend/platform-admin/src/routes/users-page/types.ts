export type UserDto = {
  userId: string;
  tenantId: string;
  username: string;
  displayName: string;
  enabled: boolean;
};

export type UsersResponse = { data: UserDto[] };
