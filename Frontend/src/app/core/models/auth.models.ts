export interface LoginRequest {
  userName: string;
  password: string;
}

export interface UserInfo {
  name: string;
  roles: string[];
}

export interface LoginResponse {
  accessToken: string;
  expiresInSeconds: number;
  user: UserInfo;
}
