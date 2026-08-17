import { request } from "./httpClient";

export interface UploadImageResponse {
  url: string;
}

export function uploadImage(file: File): Promise<UploadImageResponse> {
  const formData = new FormData();
  formData.append("file", file);

  return request<UploadImageResponse>("/api/uploads/images", {
    method: "POST",
    body: formData
  });
}
